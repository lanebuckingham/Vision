using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Infrastructure.Messaging;

/// <summary>
/// Processes a batch of unpublished outbox messages by sending them to SQS.
/// This is the production processing unit invoked by OutboxPublisher.
/// Extracted for direct integration testability.
/// </summary>
public sealed class OutboxBatchProcessor(
    IAmazonSQS sqsClient,
    ILogger<OutboxBatchProcessor> logger)
{
    public async Task<int> PublishBatchAsync(
        SecurityOperationsDbContext db,
        string queueUrl,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var messages = await db.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return 0;

        var publishedCount = 0;

        foreach (var message in messages)
        {
            try
            {
                var sendRequest = new SendMessageRequest
                {
                    QueueUrl = queueUrl,
                    MessageBody = message.Payload,
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        ["EventType"] = new()
                        {
                            DataType = "String",
                            StringValue = message.EventType
                        },
                        ["CorrelationId"] = new()
                        {
                            DataType = "String",
                            StringValue = message.CorrelationId
                        }
                    }
                };

                await sqsClient.SendMessageAsync(sendRequest, cancellationToken);

                message.PublishedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
                publishedCount++;

                logger.LogInformation(
                    "Published integration event {EventId} for correlation {CorrelationId}",
                    message.Id, message.CorrelationId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                message.AttemptCount++;
                message.LastError = ex.Message.Length > 2000
                    ? ex.Message[..2000]
                    : ex.Message;

                logger.LogWarning(ex,
                    "Failed to publish integration event {EventId}; attempt {AttemptCount}",
                    message.Id, message.AttemptCount);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return publishedCount;
    }
}
