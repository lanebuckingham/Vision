using System.Diagnostics;
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
            // Resume the distributed trace from the originating HTTP request when trace
            // context was captured at outbox-creation time; otherwise start a fresh trace.
            // A missing/invalid stored TraceParent must never prevent publication.
            using var activity = StartPublishActivity(message);

            try
            {
                var messageAttributes = new Dictionary<string, MessageAttributeValue>
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
                };

                // Prefer the live activity's current context (covers both the resumed and
                // freshly-started case) so injected context always matches the active span.
                var traceParent = activity?.Id ?? message.TraceParent;
                if (!string.IsNullOrWhiteSpace(traceParent))
                {
                    messageAttributes["traceparent"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = traceParent
                    };
                }

                var traceState = activity?.TraceStateString ?? message.TraceState;
                if (!string.IsNullOrWhiteSpace(traceState))
                {
                    messageAttributes["tracestate"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = traceState
                    };
                }

                var sendRequest = new SendMessageRequest
                {
                    QueueUrl = queueUrl,
                    MessageBody = message.Payload,
                    MessageAttributes = messageAttributes
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
                activity?.SetStatus(ActivityStatusCode.Error, "Cancelled");
                break;
            }
            catch (Exception ex)
            {
                message.AttemptCount++;
                message.LastError = ex.Message.Length > 2000
                    ? ex.Message[..2000]
                    : ex.Message;

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                logger.LogWarning(ex,
                    "Failed to publish integration event {EventId}; attempt {AttemptCount}",
                    message.Id, message.AttemptCount);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return publishedCount;
    }

    /// <summary>
    /// Starts a producer-oriented Activity for one outbox publish attempt. If the stored
    /// TraceParent is present and parses as a valid W3C context, the new activity resumes
    /// that trace; otherwise ActivitySource.StartActivity begins a fresh trace. Either way,
    /// publication proceeds — a malformed/missing stored value never blocks the send.
    /// </summary>
    private static Activity? StartPublishActivity(OutboxMessage message)
    {
        ActivityContext parentContext = default;
        var hasValidParent = !string.IsNullOrWhiteSpace(message.TraceParent)
            && ActivityContext.TryParse(message.TraceParent, message.TraceState, out parentContext);

        var activity = hasValidParent
            ? SecurityOperationsActivitySource.Instance.StartActivity(
                "IncidentCreated.v1 publish", ActivityKind.Producer, parentContext)
            : SecurityOperationsActivitySource.Instance.StartActivity(
                "IncidentCreated.v1 publish", ActivityKind.Producer);

        if (activity is null)
            return null;

        // Safe, low-cardinality messaging metadata only — never the message body.
        activity.SetTag("messaging.system", "aws_sqs");
        activity.SetTag("messaging.operation", "publish");
        activity.SetTag("messaging.destination.name", "incident-created");
        activity.SetTag("vision.event_type", message.EventType);
        activity.SetTag("vision.event_id", message.Id);

        return activity;
    }
}
