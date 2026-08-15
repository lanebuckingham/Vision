using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vision.WorkOrderService.Infrastructure.Messaging;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// Tests the production IncidentCreatedMessageProcessor code path with real PostgreSQL
/// and a mock IAmazonSQS (NSubstitute tracking delete calls).
/// Proves: DB failure → no delete, success → delete, redelivery → idempotent.
/// Requires: docker compose up -d (PostgreSQL on localhost:5432)
/// </summary>
[Collection("PostgreSQL")]
public class ConsumerProductionPathTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Database=vision_test_consumer_prod;Username=vision;Password=vision_dev";

    private WorkOrderDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new WorkOrderDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    private static IncidentCreatedV1 CreateValidEvent(Guid? eventId = null, Guid? incidentId = null) => new()
    {
        EventId = eventId ?? Guid.NewGuid(),
        EventType = IncidentCreatedV1.EventTypeName,
        OccurredAt = DateTimeOffset.UtcNow,
        CorrelationId = Guid.NewGuid().ToString("N"),
        Incident = new IncidentCreatedIncidentV1
        {
            Id = incidentId ?? Guid.NewGuid(),
            Title = "Production path test camera",
            Description = "Testing production consumer path",
            Severity = "Critical"
        },
        Asset = new IncidentCreatedAssetV1
        {
            Id = Guid.NewGuid(),
            Name = "Test Camera",
            AssetTag = "CAM-PROD",
            AssetType = "Camera"
        },
        Location = new IncidentCreatedLocationV1
        {
            Id = Guid.NewGuid(),
            Name = "Test Location",
            BuildingId = Guid.NewGuid(),
            BuildingName = "Test Building"
        }
    };

    private static Message CreateSqsMessage(IncidentCreatedV1 evt) => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        ReceiptHandle = Guid.NewGuid().ToString(),
        Body = JsonSerializer.Serialize(evt),
        Attributes = new Dictionary<string, string> { ["ApproximateReceiveCount"] = "1" }
    };

    private IServiceScopeFactory CreateScopeFactory(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContext<WorkOrderDbContext>(opts =>
            opts.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        services.AddScoped<IncidentCreatedHandler>();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task CONSUMER_REAL_001_DbFailure_DeleteNotCalled()
    {
        // Use a broken connection so the handler's DB operations fail
        var brokenScopeFactory = CreateScopeFactory(
            "Host=localhost;Port=59999;Database=nonexistent;Username=x;Password=x;Timeout=1");
        var sqsMock = Substitute.For<IAmazonSQS>();

        var processor = new IncidentCreatedMessageProcessor(
            brokenScopeFactory, sqsMock,
            NullLogger<IncidentCreatedMessageProcessor>.Instance);

        var evt = CreateValidEvent();
        var message = CreateSqsMessage(evt);

        var result = await processor.ProcessAsync(message, "http://fake/queue", CancellationToken.None);

        Assert.False(result);
        await sqsMock.DidNotReceive().DeleteMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CONSUMER_REAL_002_SuccessfulCommit_DeleteCalled()
    {
        var scopeFactory = CreateScopeFactory(ConnectionString);
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteMessageResponse()));

        var processor = new IncidentCreatedMessageProcessor(
            scopeFactory, sqsMock,
            NullLogger<IncidentCreatedMessageProcessor>.Instance);

        var evt = CreateValidEvent();
        var message = CreateSqsMessage(evt);

        var result = await processor.ProcessAsync(message, "http://fake/queue", CancellationToken.None);

        Assert.True(result);
        await sqsMock.Received(1).DeleteMessageAsync(
            "http://fake/queue", message.ReceiptHandle, Arg.Any<CancellationToken>());

        // Verify WorkOrder committed
        await using var db = CreateDbContext();
        var wo = await db.WorkOrders.FirstOrDefaultAsync(w => w.SourceEventId == evt.EventId);
        Assert.NotNull(wo);
    }

    [Fact]
    public async Task CONSUMER_REAL_003_RedeliveryAfterCommit_Idempotent_DeleteCalled()
    {
        var scopeFactory = CreateScopeFactory(ConnectionString);
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteMessageResponse()));

        var processor = new IncidentCreatedMessageProcessor(
            scopeFactory, sqsMock,
            NullLogger<IncidentCreatedMessageProcessor>.Instance);

        var evt = CreateValidEvent();

        // First delivery — creates WorkOrder
        var message1 = CreateSqsMessage(evt);
        var result1 = await processor.ProcessAsync(message1, "http://fake/queue", CancellationToken.None);
        Assert.True(result1);

        // Second delivery — same event redelivered
        var message2 = CreateSqsMessage(evt);
        message2.Attributes["ApproximateReceiveCount"] = "2";
        var result2 = await processor.ProcessAsync(message2, "http://fake/queue", CancellationToken.None);

        Assert.True(result2); // Idempotent acknowledgement
        await sqsMock.Received(2).DeleteMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Still only one WorkOrder
        await using var db = CreateDbContext();
        var count = await db.WorkOrders.CountAsync(w => w.SecurityIncidentId == evt.Incident.Id);
        Assert.Equal(1, count);
    }
}
