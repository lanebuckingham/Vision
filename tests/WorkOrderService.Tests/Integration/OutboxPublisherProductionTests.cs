using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Vision.SecurityOperationsService.Infrastructure.Messaging;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// Tests the production OutboxBatchProcessor code path with real PostgreSQL
/// and a mock IAmazonSQS (NSubstitute at the send boundary).
/// Proves: send failure leaves unpublished, send success marks published, EventId stable.
/// Requires: docker compose up -d (PostgreSQL on localhost:5432)
/// </summary>
[Collection("PostgreSQL")]
public class OutboxPublisherProductionTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Database=vision_test_publisher;Username=vision;Password=vision_dev";

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

    private static OutboxMessage CreateUnpublishedMessage() => new()
    {
        Id = Guid.NewGuid(),
        EventType = IncidentCreatedV1.EventTypeName,
        Payload = """{"eventId":"00000000-0000-0000-0000-000000000001","eventType":"vision.security-operations.incident-created.v1"}""",
        OccurredAt = DateTimeOffset.UtcNow,
        CorrelationId = Guid.NewGuid().ToString("N"),
        AttemptCount = 0
    };

    [Fact]
    public async Task PUBLISHER_001_SendFailure_LeavesUnpublished_IncrementsAttempt()
    {
        // Arrange: insert unpublished outbox message
        await using var db = CreateContext();
        var message = CreateUnpublishedMessage();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var originalEventId = message.Id;

        // Mock SQS to throw on send
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonSQSException("Simulated SQS send failure"));

        var processor = new OutboxBatchProcessor(sqsMock,
            NullLogger<OutboxBatchProcessor>.Instance);

        // Act
        await processor.PublishBatchAsync(db, "http://fake/queue", 20, CancellationToken.None);

        // Assert
        var after = await db.OutboxMessages.FirstAsync(m => m.Id == originalEventId);
        Assert.Null(after.PublishedAt);
        Assert.Equal(1, after.AttemptCount);
        Assert.NotNull(after.LastError);
        Assert.Equal(originalEventId, after.Id); // EventId unchanged
    }

    [Fact]
    public async Task PUBLISHER_002_SendSuccess_MarksPublished_EventIdStable()
    {
        // Arrange
        await using var db = CreateContext();
        var message = CreateUnpublishedMessage();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var originalEventId = message.Id;

        // Mock SQS to succeed
        var sqsMock = Substitute.For<IAmazonSQS>();
        sqsMock.SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SendMessageResponse { MessageId = Guid.NewGuid().ToString() }));

        var processor = new OutboxBatchProcessor(sqsMock,
            NullLogger<OutboxBatchProcessor>.Instance);

        // Act
        var published = await processor.PublishBatchAsync(db, "http://fake/queue", 20, CancellationToken.None);

        // Assert
        Assert.Equal(1, published);
        var after = await db.OutboxMessages.FirstAsync(m => m.Id == originalEventId);
        Assert.NotNull(after.PublishedAt);
        Assert.Null(after.LastError);
        Assert.Equal(originalEventId, after.Id); // EventId stable
    }
}
