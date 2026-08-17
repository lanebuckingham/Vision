using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vision.SecurityOperationsService.Application.Common;
using Vision.SecurityOperationsService.Application.Incidents.Commands;
using SecOpsMessaging = Vision.SecurityOperationsService.Infrastructure.Messaging;
using Vision.SecurityOperationsService.Infrastructure.Persistence;
using Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Messaging;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// Regression test for Vision's core five-minute demo story:
///
///   Critical incident for the Pharmacy Storage camera
///       -> SecurityOperationsService creates the incident + transactional outbox row
///       -> OutboxBatchProcessor publishes the real IncidentCreated.v1 contract
///       -> WorkOrderService's IncidentCreatedMessageProcessor consumes it
///       -> WorkOrder is created exactly once
///       -> WorkOrder proceeds through Assigned -> InProgress -> Completed
///
/// This is a staged component test: each service uses its own real PostgreSQL
/// persistence and the real event contract, but the SQS transport itself is mocked
/// at the boundary (send captures the message; receive replays exactly what was
/// captured) rather than requiring a live SQS/LocalStack round trip. This protects
/// the demo story without an artificial cross-service distributed transaction.
/// Requires: docker compose up -d (PostgreSQL on localhost:5432)
/// </summary>
[Collection("PostgreSQL")]
public class PrimaryDemoPathTests : IAsyncLifetime
{
    private const string SecOpsConnectionString =
        "Host=localhost;Database=vision_test_demo_secops;Username=vision;Password=vision_dev";
    private const string WorkOrderConnectionString =
        "Host=localhost;Database=vision_test_demo_workorder;Username=vision;Password=vision_dev";

    private SecurityOperationsDbContext CreateSecOpsContext()
    {
        var options = new DbContextOptionsBuilder<SecurityOperationsDbContext>()
            .UseNpgsql(SecOpsConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new SecurityOperationsDbContext(options);
    }

    private WorkOrderDbContext CreateWorkOrderContext()
    {
        var options = new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseNpgsql(WorkOrderConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new WorkOrderDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var secOpsDb = CreateSecOpsContext();
        await secOpsDb.Database.EnsureDeletedAsync();
        await secOpsDb.Database.EnsureCreatedAsync();
        await SecurityOperationsSeeder.SeedAsync(secOpsDb);

        await using var workOrderDb = CreateWorkOrderContext();
        await workOrderDb.Database.EnsureDeletedAsync();
        await workOrderDb.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var secOpsDb = CreateSecOpsContext();
        await secOpsDb.Database.EnsureDeletedAsync();

        await using var workOrderDb = CreateWorkOrderContext();
        await workOrderDb.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task CriticalPharmacyIncident_FlowsThroughOutboxAndSqsIntoExactlyOneWorkOrder()
    {
        // --- Step 1: SecurityOperationsService — create the Critical incident ---
        await using var secOpsDb = CreateSecOpsContext();

        var pharmacyCameraId = SecurityOperationsService.Infrastructure.Persistence.Seeding.SeedDataIds.PharmacyStorageCamera02;
        var camera = await secOpsDb.SecurityAssets.AsNoTracking().FirstAsync(a => a.Id == pharmacyCameraId);

        var correlationCtx = new CorrelationContext { CorrelationId = "demo-path-correlation" };
        var createHandler = new CreateIncidentCommandHandler(
            secOpsDb, correlationCtx, NullLogger<CreateIncidentCommandHandler>.Instance);

        var createCommand = new CreateIncidentCommand(
            LocationId: camera.LocationId,
            AssetId: pharmacyCameraId,
            Title: "Pharmacy storage camera offline",
            Description: "Camera stopped responding. Pharmacy storage has no visual coverage.",
            Severity: "Critical");

        var incidentResult = await createHandler.Handle(createCommand, CancellationToken.None);

        var outboxMessage = await secOpsDb.OutboxMessages.FirstAsync();
        Assert.Equal(SecOpsMessaging.IncidentCreatedV1.EventTypeName, outboxMessage.EventType);
        Assert.Null(outboxMessage.PublishedAt);

        // --- Step 2: SecurityOperationsService — publish via the real OutboxBatchProcessor,
        // capturing the exact SQS SendMessage request instead of hitting real SQS ---
        SendMessageRequest? capturedSend = null;
        var producerSqsMock = Substitute.For<IAmazonSQS>();
        producerSqsMock.SendMessageAsync(Arg.Do<SendMessageRequest>(r => capturedSend = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SendMessageResponse { MessageId = Guid.NewGuid().ToString() }));

        var outboxProcessor = new SecOpsMessaging.OutboxBatchProcessor(producerSqsMock, NullLogger<SecOpsMessaging.OutboxBatchProcessor>.Instance);
        var publishedCount = await outboxProcessor.PublishBatchAsync(secOpsDb, "http://fake/queue", 20, CancellationToken.None);

        Assert.Equal(1, publishedCount);
        Assert.NotNull(capturedSend);

        var republishedOutbox = await secOpsDb.OutboxMessages.FirstAsync();
        Assert.NotNull(republishedOutbox.PublishedAt);

        // --- Step 3: WorkOrderService — consume the real contract payload that was sent ---
        await using var workOrderDb = CreateWorkOrderContext();

        var sqsMessage = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            ReceiptHandle = Guid.NewGuid().ToString(),
            Body = capturedSend!.MessageBody,
            Attributes = new Dictionary<string, string> { ["ApproximateReceiveCount"] = "1" },
            MessageAttributes = capturedSend.MessageAttributes
        };

        var consumerSqsMock = Substitute.For<IAmazonSQS>();
        consumerSqsMock.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteMessageResponse()));

        var scopeFactory = BuildWorkOrderScopeFactory();
        var messageProcessor = new IncidentCreatedMessageProcessor(
            scopeFactory, consumerSqsMock, NullLogger<IncidentCreatedMessageProcessor>.Instance);

        var acknowledged = await messageProcessor.ProcessAsync(sqsMessage, "http://fake/queue", CancellationToken.None);
        Assert.True(acknowledged);

        // --- Step 4: WorkOrder created exactly once, with the real contract data preserved ---
        var workOrders = await workOrderDb.WorkOrders
            .Where(w => w.SecurityIncidentId == incidentResult.Id)
            .ToListAsync();

        Assert.Single(workOrders);
        var workOrder = workOrders[0];
        Assert.Equal(WorkOrderStatus.New, workOrder.Status);
        Assert.Equal(WorkOrderPriority.Critical, workOrder.Priority);
        Assert.Equal(pharmacyCameraId, workOrder.SecurityAssetId);
        Assert.Equal("demo-path-correlation", workOrder.CorrelationId);
        Assert.Equal(outboxMessage.Id, workOrder.SourceEventId);

        // Duplicate delivery of the same message must remain idempotent — no second WorkOrder.
        var duplicateAck = await messageProcessor.ProcessAsync(sqsMessage, "http://fake/queue", CancellationToken.None);
        Assert.True(duplicateAck);
        var countAfterDuplicate = await workOrderDb.WorkOrders
            .CountAsync(w => w.SecurityIncidentId == incidentResult.Id);
        Assert.Equal(1, countAfterDuplicate);

        // --- Step 5: continue through the WorkOrder lifecycle a Technician would drive ---
        var technician = new Technician
        {
            Id = Guid.NewGuid(),
            DisplayName = "Demo Path Technician",
            Email = "demo.path.tech@northstarmedical.com",
            IsActive = true,
            Specialty = "Camera & Video Systems",
            CognitoSubject = $"cognito-demo-path-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30)
        };
        workOrderDb.Technicians.Add(technician);
        await workOrderDb.SaveChangesAsync();

        var trackedWorkOrder = await workOrderDb.WorkOrders
            .Include(w => w.Notes)
            .FirstAsync(w => w.Id == workOrder.Id);

        trackedWorkOrder.AssignTechnician(technician);
        await workOrderDb.SaveChangesAsync();
        Assert.Equal(WorkOrderStatus.Assigned, trackedWorkOrder.Status);

        trackedWorkOrder.StartWork();
        await workOrderDb.SaveChangesAsync();
        Assert.Equal(WorkOrderStatus.InProgress, trackedWorkOrder.Status);

        trackedWorkOrder.AddNote(technician.Id, "Replaced the camera and verified the feed over a full recording cycle.");
        trackedWorkOrder.Complete(null);
        await workOrderDb.SaveChangesAsync();

        Assert.Equal(WorkOrderStatus.Completed, trackedWorkOrder.Status);
        Assert.NotNull(trackedWorkOrder.CompletedAt);
        Assert.Single(trackedWorkOrder.Notes);
    }

    private static IServiceScopeFactory BuildWorkOrderScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContext<WorkOrderDbContext>(opts =>
            opts.UseNpgsql(WorkOrderConnectionString).UseSnakeCaseNamingConvention());
        services.AddScoped<IncidentCreatedHandler>();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }
}
