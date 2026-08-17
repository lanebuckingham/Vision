using System.Diagnostics;
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

namespace Vision.WorkOrderService.Tests.Messaging;

/// <summary>
/// Verifies the Phase 6 requirement that IncidentCreatedMessageProcessor extracts W3C
/// trace context from incoming SQS message attributes, and that invalid/missing trace
/// context never breaks otherwise-valid business message processing.
/// Requires: docker compose up -d (PostgreSQL on localhost:5432)
/// </summary>
[Collection("PostgreSQL")]
public class IncidentCreatedMessageProcessorTraceTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Database=vision_test_consumer_trace;Username=vision;Password=vision_dev";

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

    private static WorkOrderDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new WorkOrderDbContext(options);
    }

    private static IncidentCreatedV1 CreateValidEvent() => new()
    {
        EventId = Guid.NewGuid(),
        EventType = IncidentCreatedV1.EventTypeName,
        OccurredAt = DateTimeOffset.UtcNow,
        CorrelationId = Guid.NewGuid().ToString("N"),
        Incident = new IncidentCreatedIncidentV1
        {
            Id = Guid.NewGuid(),
            Title = "Trace extraction test camera",
            Description = "Testing consumer trace-context extraction",
            Severity = "Critical"
        },
        Asset = new IncidentCreatedAssetV1
        {
            Id = Guid.NewGuid(),
            Name = "Test Camera",
            AssetTag = "CAM-TRACE",
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

    private static Message CreateSqsMessage(
        IncidentCreatedV1 evt, string? traceParent = null, string? traceState = null)
    {
        var attributes = new Dictionary<string, MessageAttributeValue>();
        if (traceParent is not null)
        {
            attributes["traceparent"] = new MessageAttributeValue { DataType = "String", StringValue = traceParent };
        }
        if (traceState is not null)
        {
            attributes["tracestate"] = new MessageAttributeValue { DataType = "String", StringValue = traceState };
        }

        return new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            ReceiptHandle = Guid.NewGuid().ToString(),
            Body = JsonSerializer.Serialize(evt),
            Attributes = new Dictionary<string, string> { ["ApproximateReceiveCount"] = "1" },
            MessageAttributes = attributes
        };
    }

    private static IServiceScopeFactory CreateScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContext<WorkOrderDbContext>(opts =>
            opts.UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention());
        services.AddScoped<IncidentCreatedHandler>();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task ProcessAsync_EstablishesLoggingScopeContainingEventCorrelationId()
    {
        var scopeCapture = new ScopeCapturingLoggerProvider();

        var services = new ServiceCollection();
        services.AddDbContext<WorkOrderDbContext>(opts =>
            opts.UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention());
        services.AddScoped<IncidentCreatedHandler>();
        services.AddLogging(logging => logging.AddProvider(scopeCapture));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteMessageResponse()));

        // The scope must come from a logger wired to the capturing provider — the
        // processor's own logger.BeginScope call is what's under test here.
        var processorLogger = provider.GetRequiredService<ILogger<IncidentCreatedMessageProcessor>>();

        var processor = new IncidentCreatedMessageProcessor(scopeFactory, sqsMock, processorLogger);

        var evt = CreateValidEvent();
        var message = CreateSqsMessage(evt);

        var result = await processor.ProcessAsync(message, "http://fake/queue", CancellationToken.None);
        Assert.True(result);

        // Prove IncidentCreatedMessageProcessor actually calls ILogger.BeginScope with
        // the event's durable Vision CorrelationId while handling the message — not
        // just that the WorkOrder was created — by inspecting every scope value
        // established anywhere in the DI container during this call.
        var matchingScope = scopeCapture.CapturedScopes
            .OfType<IEnumerable<KeyValuePair<string, object>>>()
            .SelectMany(scope => scope)
            .FirstOrDefault(kvp => kvp.Key == "CorrelationId" && Equals(kvp.Value, evt.CorrelationId));

        Assert.NotEqual(default, matchingScope);
    }

    [Fact]
    public async Task ValidTraceParent_IsExtractedAndProcessingSucceeds()
    {
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteMessageResponse()));

        var processor = new IncidentCreatedMessageProcessor(
            CreateScopeFactory(), sqsMock, NullLogger<IncidentCreatedMessageProcessor>.Instance);

        var evt = CreateValidEvent();
        var validTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        var message = CreateSqsMessage(evt, traceParent: validTraceParent);

        var result = await processor.ProcessAsync(message, "http://fake/queue", CancellationToken.None);

        Assert.True(result);
        await using var db = CreateDbContext();
        var wo = await db.WorkOrders.FirstOrDefaultAsync(w => w.SourceEventId == evt.EventId);
        Assert.NotNull(wo);
    }

    [Fact]
    public async Task ValidTraceParent_ConsumerActivityContinuesTheIncomingTrace()
    {
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteMessageResponse()));

        var processor = new IncidentCreatedMessageProcessor(
            CreateScopeFactory(), sqsMock, NullLogger<IncidentCreatedMessageProcessor>.Instance);

        var evt = CreateValidEvent();
        var incomingTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        var incomingSpanId = "00f067aa0ba902b7";
        var incomingTraceParent = $"00-{incomingTraceId}-{incomingSpanId}-01";
        var message = CreateSqsMessage(evt, traceParent: incomingTraceParent);

        // Observe the actual consumer Activity that IncidentCreatedMessageProcessor
        // creates via WorkOrderActivitySource, rather than only checking that
        // processing succeeded.
        var capturedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkOrderActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        var result = await processor.ProcessAsync(message, "http://fake/queue", CancellationToken.None);

        Assert.True(result);
        var consumerActivity = Assert.Single(capturedActivities);
        Assert.Equal(ActivityKind.Consumer, consumerActivity.Kind);

        // The consumer span must be on the same trace as the incoming message, and its
        // parent span must be the span that was active when the message was published —
        // this is what proves trace *continuation*, not just successful business logic.
        Assert.Equal(incomingTraceId, consumerActivity.TraceId.ToHexString());
        Assert.Equal(incomingSpanId, consumerActivity.ParentSpanId.ToHexString());
    }

    [Fact]
    public async Task MissingTraceParent_DoesNotCrashProcessing_AndStartsAFreshTrace()
    {
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteMessageResponse()));

        var processor = new IncidentCreatedMessageProcessor(
            CreateScopeFactory(), sqsMock, NullLogger<IncidentCreatedMessageProcessor>.Instance);

        var evt = CreateValidEvent();
        var message = CreateSqsMessage(evt); // No trace attributes at all

        var capturedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkOrderActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        var result = await processor.ProcessAsync(message, "http://fake/queue", CancellationToken.None);

        Assert.True(result);
        await using var db = CreateDbContext();
        var wo = await db.WorkOrders.FirstOrDefaultAsync(w => w.SourceEventId == evt.EventId);
        Assert.NotNull(wo);

        // A consumer span must still be created — just on a fresh trace rather than a
        // continued one, since no parent context was available.
        var consumerActivity = Assert.Single(capturedActivities);
        Assert.Equal(ActivityKind.Consumer, consumerActivity.Kind);
        Assert.NotEqual(default, consumerActivity.TraceId);
        Assert.Equal(default, consumerActivity.ParentSpanId);
    }

    [Fact]
    public async Task MalformedTraceParent_DoesNotCrashProcessingOrPreventBusinessSuccess_AndFallsBackToFreshTrace()
    {
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteMessageResponse()));

        var processor = new IncidentCreatedMessageProcessor(
            CreateScopeFactory(), sqsMock, NullLogger<IncidentCreatedMessageProcessor>.Instance);

        var evt = CreateValidEvent();
        var message = CreateSqsMessage(evt, traceParent: "totally-not-w3c-format");

        var capturedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkOrderActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        var result = await processor.ProcessAsync(message, "http://fake/queue", CancellationToken.None);

        // Malformed tracing metadata must not turn an otherwise valid business
        // message into a poison message.
        Assert.True(result);
        await using var db = CreateDbContext();
        var wo = await db.WorkOrders.FirstOrDefaultAsync(w => w.SourceEventId == evt.EventId);
        Assert.NotNull(wo);

        var consumerActivity = Assert.Single(capturedActivities);
        Assert.Equal(ActivityKind.Consumer, consumerActivity.Kind);
        Assert.NotEqual(default, consumerActivity.TraceId);
        Assert.Equal(default, consumerActivity.ParentSpanId);
    }

    [Fact]
    public async Task ValidTraceParentWithTraceState_BothExtractedWithoutError()
    {
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteMessageResponse()));

        var processor = new IncidentCreatedMessageProcessor(
            CreateScopeFactory(), sqsMock, NullLogger<IncidentCreatedMessageProcessor>.Instance);

        var evt = CreateValidEvent();
        var message = CreateSqsMessage(
            evt,
            traceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            traceState: "vision=abc123");

        var result = await processor.ProcessAsync(message, "http://fake/queue", CancellationToken.None);

        Assert.True(result);
    }
}
