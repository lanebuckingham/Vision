using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vision.SecurityOperationsService.Infrastructure.Messaging;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// Verifies the Phase 6 requirement that OutboxBatchProcessor injects W3C trace
/// context into SQS message attributes when trace context exists, while preserving
/// the existing CorrelationId/EventType attributes, and that publication proceeds
/// normally when no stored trace context is present.
/// Requires: docker compose up -d (PostgreSQL on localhost:5432)
/// </summary>
[Collection("PostgreSQL")]
public class OutboxPublisherTraceInjectionTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Database=vision_test_publisher_trace;Username=vision;Password=vision_dev";

    private SecurityOperationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SecurityOperationsDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new SecurityOperationsDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    private static OutboxMessage CreateMessage(string? traceParent = null, string? traceState = null) => new()
    {
        Id = Guid.NewGuid(),
        EventType = IncidentCreatedV1.EventTypeName,
        Payload = """{"eventId":"00000000-0000-0000-0000-000000000001","eventType":"vision.security-operations.incident-created.v1"}""",
        OccurredAt = DateTimeOffset.UtcNow,
        CorrelationId = Guid.NewGuid().ToString("N"),
        TraceParent = traceParent,
        TraceState = traceState
    };

    [Fact]
    public async Task ValidStoredTraceParent_IsInjectedAsSqsMessageAttribute()
    {
        await using var db = CreateContext();
        // A syntactically valid W3C traceparent: version-traceid(32 hex)-spanid(16 hex)-flags
        var validTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        var message = CreateMessage(traceParent: validTraceParent);
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        SendMessageRequest? capturedRequest = null;
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.SendMessageAsync(Arg.Do<SendMessageRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SendMessageResponse { MessageId = Guid.NewGuid().ToString() }));

        var processor = new OutboxBatchProcessor(sqsMock, NullLogger<OutboxBatchProcessor>.Instance);
        await processor.PublishBatchAsync(db, "http://fake/queue", 20, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest!.MessageAttributes.ContainsKey("traceparent"));

        // Existing attributes must remain present alongside the new trace attributes.
        Assert.True(capturedRequest.MessageAttributes.ContainsKey("CorrelationId"));
        Assert.Equal(message.CorrelationId, capturedRequest.MessageAttributes["CorrelationId"].StringValue);
        Assert.True(capturedRequest.MessageAttributes.ContainsKey("EventType"));
    }

    [Fact]
    public async Task ValidStoredTraceParent_ProducerActivityResumesOriginatingTrace_AndInjectedAttributeMatchesIt()
    {
        await using var db = CreateContext();
        var storedTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        var storedTraceParent = $"00-{storedTraceId}-00f067aa0ba902b7-01";
        var message = CreateMessage(traceParent: storedTraceParent);
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        // Observe the actual producer Activity that OutboxBatchProcessor creates via
        // SecurityOperationsActivitySource, rather than only inspecting SQS attributes.
        var capturedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SecurityOperationsActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        SendMessageRequest? capturedRequest = null;
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.SendMessageAsync(Arg.Do<SendMessageRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SendMessageResponse { MessageId = Guid.NewGuid().ToString() }));

        var processor = new OutboxBatchProcessor(sqsMock, NullLogger<OutboxBatchProcessor>.Instance);
        await processor.PublishBatchAsync(db, "http://fake/queue", 20, CancellationToken.None);

        var producerActivity = Assert.Single(capturedActivities);
        Assert.Equal(ActivityKind.Producer, producerActivity.Kind);

        // The producer span must resume the originating trace, not start an unrelated one.
        Assert.Equal(storedTraceId, producerActivity.TraceId.ToHexString());

        // The traceparent injected into the SQS message must correspond to this same
        // producer Activity's context — not merely be "present" — so a consumer that
        // extracts it truly continues the same distributed trace.
        Assert.NotNull(capturedRequest);
        var injectedTraceParent = capturedRequest!.MessageAttributes["traceparent"].StringValue;
        Assert.Equal(producerActivity.Id, injectedTraceParent);
        Assert.Contains(storedTraceId, injectedTraceParent);
    }

    [Fact]
    public async Task NoStoredTraceContext_ProducerActivityStartsNewTrace()
    {
        await using var db = CreateContext();
        var message = CreateMessage(traceParent: null, traceState: null);
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var capturedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SecurityOperationsActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SendMessageResponse { MessageId = Guid.NewGuid().ToString() }));

        var processor = new OutboxBatchProcessor(sqsMock, NullLogger<OutboxBatchProcessor>.Instance);
        await processor.PublishBatchAsync(db, "http://fake/queue", 20, CancellationToken.None);

        var producerActivity = Assert.Single(capturedActivities);
        Assert.Equal(ActivityKind.Producer, producerActivity.Kind);
        // No stored parent — the producer must still create a valid (fresh) trace,
        // not fail or produce a default/empty TraceId.
        Assert.NotEqual(default, producerActivity.TraceId);
    }

    [Fact]
    public async Task NoStoredTraceContext_StillPublishesSuccessfully()
    {
        await using var db = CreateContext();
        var message = CreateMessage(traceParent: null, traceState: null);
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SendMessageResponse { MessageId = Guid.NewGuid().ToString() }));

        var processor = new OutboxBatchProcessor(sqsMock, NullLogger<OutboxBatchProcessor>.Instance);
        var published = await processor.PublishBatchAsync(db, "http://fake/queue", 20, CancellationToken.None);

        Assert.Equal(1, published);
        var after = await db.OutboxMessages.FirstAsync(m => m.Id == message.Id);
        Assert.NotNull(after.PublishedAt);
    }

    [Fact]
    public async Task MalformedStoredTraceParent_DoesNotBlockPublication()
    {
        await using var db = CreateContext();
        var message = CreateMessage(traceParent: "not-a-valid-w3c-traceparent");
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SendMessageResponse { MessageId = Guid.NewGuid().ToString() }));

        var processor = new OutboxBatchProcessor(sqsMock, NullLogger<OutboxBatchProcessor>.Instance);
        var published = await processor.PublishBatchAsync(db, "http://fake/queue", 20, CancellationToken.None);

        // Malformed tracing metadata must never turn an otherwise valid business
        // message into a poison message.
        Assert.Equal(1, published);
        var after = await db.OutboxMessages.FirstAsync(m => m.Id == message.Id);
        Assert.NotNull(after.PublishedAt);
        Assert.Null(after.LastError);
    }
}
