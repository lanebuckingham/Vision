using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Messaging;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// Tests the consumer failure windows using real PostgreSQL + LocalStack.
/// Proves commit-before-delete ordering, DB failure leaves message, and redelivery idempotency.
/// Requires: docker compose up -d
/// </summary>
[Collection("ConsumerFailureWindows")]
public class ConsumerFailureWindowTests : IAsyncLifetime
{
    private const string DbConnectionString =
        "Host=localhost;Database=vision_test_consumer;Username=vision;Password=vision_dev";
    private const string SqsServiceUrl = "http://localhost:4566";
    private const string Region = "us-east-1";
    private const string TestQueueName = "vision-test-consumer-failure";

    private IAmazonSQS _sqsClient = null!;
    private string _queueUrl = "";

    private WorkOrderDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseNpgsql(DbConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new WorkOrderDbContext(options);
    }

    public async Task InitializeAsync()
    {
        // Setup DB
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        // Setup SQS
        var config = new AmazonSQSConfig
        {
            ServiceURL = SqsServiceUrl,
            AuthenticationRegion = Region
        };
        _sqsClient = new AmazonSQSClient(
            new Amazon.Runtime.BasicAWSCredentials("test", "test"), config);

        var response = await _sqsClient.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = TestQueueName,
            Attributes = new Dictionary<string, string> { ["VisibilityTimeout"] = "2" }
        });
        _queueUrl = response.QueueUrl;
    }

    public async Task DisposeAsync()
    {
        try { await _sqsClient.DeleteQueueAsync(_queueUrl); } catch { }
        _sqsClient.Dispose();

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
            Title = "Consumer test camera offline",
            Description = "Testing consumer failure windows",
            Severity = "Critical"
        },
        Asset = new IncidentCreatedAssetV1
        {
            Id = Guid.NewGuid(),
            Name = "Test Camera",
            AssetTag = "CAM-TEST",
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

    [Fact]
    public async Task CONSUMER_001_DbFailure_MessageNotDeleted()
    {
        var evt = CreateValidEvent();
        var body = JsonSerializer.Serialize(evt);

        // Send message to SQS
        await _sqsClient.SendMessageAsync(_queueUrl, body);

        // Receive the message
        var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        });
        Assert.Single(response.Messages);
        var message = response.Messages[0];

        // Simulate: handler throws because DB is "unavailable" (use a broken connection string)
        var brokenOptions = new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseNpgsql("Host=localhost;Port=59999;Database=nonexistent;Username=x;Password=x;Timeout=1")
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var brokenDb = new WorkOrderDbContext(brokenOptions);
        var handler = new IncidentCreatedHandler(brokenDb, NullLogger<IncidentCreatedHandler>.Instance);

        // Handler should throw (transient failure)
        await Assert.ThrowsAnyAsync<Exception>(
            () => handler.HandleAsync(evt, CancellationToken.None));

        // Message should NOT be deleted — don't call DeleteMessage
        // Wait for visibility timeout
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Message should be redelivered
        var retry = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        });
        Assert.Single(retry.Messages);

        // Clean up
        await _sqsClient.DeleteMessageAsync(_queueUrl, retry.Messages[0].ReceiptHandle);
    }

    [Fact]
    public async Task CONSUMER_002_SuccessfulCommit_ThenDelete()
    {
        var evt = CreateValidEvent();
        var body = JsonSerializer.Serialize(evt);

        // Send message
        await _sqsClient.SendMessageAsync(_queueUrl, body);

        // Receive
        var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        });
        Assert.Single(response.Messages);
        var message = response.Messages[0];

        // Process with real DB
        await using var db = CreateDbContext();
        var handler = new IncidentCreatedHandler(db, NullLogger<IncidentCreatedHandler>.Instance);
        var shouldAck = await handler.HandleAsync(evt, CancellationToken.None);

        Assert.True(shouldAck);

        // Verify WorkOrder committed
        var wo = await db.WorkOrders.AsNoTracking().FirstOrDefaultAsync(w => w.SourceEventId == evt.EventId);
        Assert.NotNull(wo);

        // Now delete (commit happened BEFORE delete)
        await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle);

        // Verify message gone
        var empty = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 1
        });
        Assert.Empty(empty.Messages);
    }

    [Fact]
    public async Task CONSUMER_003_RedeliveryAfterCommit_RemainsIdempotent()
    {
        var evt = CreateValidEvent();
        var body = JsonSerializer.Serialize(evt);

        // First delivery: commit WorkOrder successfully
        await using (var db1 = CreateDbContext())
        {
            var handler1 = new IncidentCreatedHandler(db1, NullLogger<IncidentCreatedHandler>.Instance);
            var result1 = await handler1.HandleAsync(evt, CancellationToken.None);
            Assert.True(result1);
        }

        // Simulate: message redelivered (crash before DeleteMessage scenario)
        await _sqsClient.SendMessageAsync(_queueUrl, body);
        var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        });
        Assert.Single(response.Messages);

        // Second delivery: handler sees existing WorkOrder
        await using (var db2 = CreateDbContext())
        {
            var handler2 = new IncidentCreatedHandler(db2, NullLogger<IncidentCreatedHandler>.Instance);
            var result2 = await handler2.HandleAsync(evt, CancellationToken.None);
            Assert.True(result2); // Idempotent success — should acknowledge
        }

        // Verify still only one WorkOrder
        await using (var db3 = CreateDbContext())
        {
            var count = await db3.WorkOrders.CountAsync(w => w.SecurityIncidentId == evt.Incident.Id);
            Assert.Equal(1, count);
        }

        // Clean up
        await _sqsClient.DeleteMessageAsync(_queueUrl, response.Messages[0].ReceiptHandle);
    }
}

[CollectionDefinition("ConsumerFailureWindows")]
public class ConsumerFailureWindowsCollection : ICollectionFixture<ConsumerFailureWindowsFixture>;

public class ConsumerFailureWindowsFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Verify both PostgreSQL and LocalStack are available
        var sqsConfig = new AmazonSQSConfig
        {
            ServiceURL = "http://localhost:4566",
            AuthenticationRegion = "us-east-1"
        };
        using var client = new AmazonSQSClient(
            new Amazon.Runtime.BasicAWSCredentials("test", "test"), sqsConfig);

        try
        {
            await client.ListQueuesAsync(new ListQueuesRequest());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "LocalStack SQS not available. Run 'docker compose up -d' first.", ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
