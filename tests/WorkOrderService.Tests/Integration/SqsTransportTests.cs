using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Vision.WorkOrderService.Infrastructure.Messaging;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// Transport-level tests using LocalStack SQS.
/// Requires: docker compose up -d (vision-localstack running on localhost:4566)
/// These tests use a dedicated test queue to avoid interfering with development queues.
/// </summary>
[Collection("LocalStack")]
public class SqsTransportTests : IAsyncLifetime
{
    private const string ServiceUrl = "http://localhost:4566";
    private const string Region = "us-east-1";
    private const string TestQueueName = "vision-test-transport";
    private const string TestDlqName = "vision-test-transport-dlq";

    private IAmazonSQS _sqsClient = null!;
    private string _queueUrl = "";
    private string _dlqUrl = "";

    public async Task InitializeAsync()
    {
        var config = new AmazonSQSConfig
        {
            ServiceURL = ServiceUrl,
            AuthenticationRegion = Region
        };
        _sqsClient = new AmazonSQSClient(
            new Amazon.Runtime.BasicAWSCredentials("test", "test"), config);

        // Create DLQ
        var dlqResponse = await _sqsClient.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = TestDlqName,
            Attributes = new Dictionary<string, string>
            {
                ["MessageRetentionPeriod"] = "1209600"
            }
        });
        _dlqUrl = dlqResponse.QueueUrl;

        var dlqArnResponse = await _sqsClient.GetQueueAttributesAsync(
            _dlqUrl, ["QueueArn"]);
        var dlqArn = dlqArnResponse.Attributes["QueueArn"];

        // Create primary queue with short visibility for faster test turnaround
        var redrivePolicy = JsonSerializer.Serialize(new { deadLetterTargetArn = dlqArn, maxReceiveCount = "2" });
        var queueResponse = await _sqsClient.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = TestQueueName,
            Attributes = new Dictionary<string, string>
            {
                ["VisibilityTimeout"] = "2",
                ["RedrivePolicy"] = redrivePolicy
            }
        });
        _queueUrl = queueResponse.QueueUrl;
    }

    public async Task DisposeAsync()
    {
        try { await _sqsClient.DeleteQueueAsync(_queueUrl); } catch { }
        try { await _sqsClient.DeleteQueueAsync(_dlqUrl); } catch { }
        _sqsClient.Dispose();
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
            Title = "Test camera offline",
            Description = "Camera stopped",
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
    public async Task SendAndReceive_ValidMessage_CanBeDeleted()
    {
        var evt = CreateValidEvent();
        var body = JsonSerializer.Serialize(evt);

        await _sqsClient.SendMessageAsync(_queueUrl, body);

        var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        });

        Assert.Single(response.Messages);
        var received = JsonSerializer.Deserialize<IncidentCreatedV1>(response.Messages[0].Body);
        Assert.NotNull(received);
        Assert.Equal(evt.EventId, received.EventId);

        // Delete after processing
        await _sqsClient.DeleteMessageAsync(_queueUrl, response.Messages[0].ReceiptHandle);

        // Verify no more messages
        var empty = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 1
        });
        Assert.Empty(empty.Messages);
    }

    [Fact]
    public async Task UnacknowledgedMessage_BecomesVisibleAgain()
    {
        var body = JsonSerializer.Serialize(CreateValidEvent());

        await _sqsClient.SendMessageAsync(_queueUrl, body);

        // Receive but don't delete
        var first = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        });
        Assert.Single(first.Messages);

        // Wait for visibility timeout (2s) + buffer
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Message should be redelivered
        var second = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        });
        Assert.Single(second.Messages);

        // Clean up
        await _sqsClient.DeleteMessageAsync(_queueUrl, second.Messages[0].ReceiptHandle);
    }

    [Fact]
    public async Task PoisonMessage_MovesToDlq_AfterMaxReceiveCount()
    {
        var poisonBody = "{\"eventType\":\"vision.security-operations.incident-created.v2\"}";

        await _sqsClient.SendMessageAsync(_queueUrl, poisonBody);

        // Receive maxReceiveCount (2) times without deleting
        for (var i = 0; i < 3; i++)
        {
            var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = _queueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 3
            });

            if (response.Messages.Count == 0)
                break;

            // Don't delete — simulate failure, wait for visibility timeout (2s)
            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        // Give LocalStack time to process redrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Check DLQ
        var dlqResponse = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _dlqUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        });

        Assert.Single(dlqResponse.Messages);
        Assert.Equal(poisonBody, dlqResponse.Messages[0].Body); // Original payload preserved

        // Clean up DLQ
        await _sqsClient.DeleteMessageAsync(_dlqUrl, dlqResponse.Messages[0].ReceiptHandle);
    }
}

[CollectionDefinition("LocalStack")]
public class LocalStackCollection : ICollectionFixture<LocalStackFixture>;

/// <summary>
/// Verifies LocalStack SQS is available before running transport tests.
/// </summary>
public class LocalStackFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var config = new AmazonSQSConfig
        {
            ServiceURL = "http://localhost:4566",
            AuthenticationRegion = "us-east-1"
        };
        using var client = new AmazonSQSClient(
            new Amazon.Runtime.BasicAWSCredentials("test", "test"), config);

        // Verify SQS is reachable
        try
        {
            await client.ListQueuesAsync(new ListQueuesRequest());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "LocalStack SQS is not available at localhost:4566. Run 'docker compose up -d' first.", ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
