using System.Diagnostics;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Vision.WorkOrderService.Infrastructure.Messaging;

/// <summary>
/// Processes a single SQS message containing an IncidentCreated event.
/// This is the production processing unit invoked by IncidentCreatedConsumer.
/// Extracted for direct integration testability.
///
/// Owns: deserialization, validation, handler invocation, and the DeleteMessage decision.
/// </summary>
public sealed class IncidentCreatedMessageProcessor(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqsClient,
    ILogger<IncidentCreatedMessageProcessor> logger)
{
    /// <summary>
    /// Processes one SQS message through the full production path.
    /// Returns true if the message was successfully acknowledged (deleted).
    /// </summary>
    public async Task<bool> ProcessAsync(Message message, string queueUrl, CancellationToken cancellationToken)
    {
        var receiveCount = message.Attributes.GetValueOrDefault("ApproximateReceiveCount", "?");

        // Extract W3C trace context from SQS message attributes, if present, so this
        // message's processing continues the distributed trace started by the
        // originating HTTP request. Invalid or missing context must not crash the
        // consumer — processing simply starts a new trace instead.
        using var activity = StartConsumeActivity(message, receiveCount);

        IncidentCreatedV1? evt = null;

        try
        {
            evt = JsonSerializer.Deserialize<IncidentCreatedV1>(message.Body);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize SQS message {MessageId} (receive #{ReceiveCount}): invalid JSON",
                message.MessageId, receiveCount);
            activity?.SetStatus(ActivityStatusCode.Error, "Malformed JSON body");
            return false;
        }

        if (evt is null)
        {
            logger.LogWarning("Deserialized null event from SQS message {MessageId} (receive #{ReceiveCount})",
                message.MessageId, receiveCount);
            activity?.SetStatus(ActivityStatusCode.Error, "Deserialized null event");
            return false;
        }

        activity?.SetTag("vision.event_id", evt.EventId);

        if (evt.EventType != IncidentCreatedV1.EventTypeName)
        {
            logger.LogWarning(
                "Unsupported event type '{EventType}' in message {MessageId} (receive #{ReceiveCount}). Expected {Expected}",
                evt.EventType, message.MessageId, receiveCount, IncidentCreatedV1.EventTypeName);
            activity?.SetStatus(ActivityStatusCode.Error, "Unsupported event type");
            return false;
        }

        logger.LogInformation(
            "Received IncidentCreated event {EventId} for incident {IncidentId} (receive #{ReceiveCount})",
            evt.EventId, evt.Incident?.Id, receiveCount);

        // Establish a logging scope keyed by the durable Vision CorrelationId (now that
        // the event has been safely deserialized/validated) so every log line emitted
        // while handling this message — across the handler, EF Core, DeleteMessage
        // decision, etc. — can be searched by CorrelationId without repeating it at
        // every call site. Complements, not replaces, the explicit EventId/IncidentId
        // properties already logged above.
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = evt.CorrelationId
        }))
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IncidentCreatedHandler>();

            try
            {
                var shouldAcknowledge = await handler.HandleAsync(evt, cancellationToken);

                if (shouldAcknowledge)
                {
                    await sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);
                    return true;
                }

                activity?.SetStatus(ActivityStatusCode.Error, "Handler rejected the event; left for DLQ redrive");
                return false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown — not an application failure.
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process event {EventId} from message {MessageId} (receive #{ReceiveCount})",
                    evt.EventId, message.MessageId, receiveCount);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Starts a consumer-oriented Activity covering one message's processing: extract
    /// trace context, deserialize, validate, invoke the handler, and the DeleteMessage
    /// decision. Does not span the infinite background polling loop itself.
    /// </summary>
    private static Activity? StartConsumeActivity(Message message, string receiveCount)
    {
        ActivityContext parentContext = default;
        var hasValidParent = false;

        if (message.MessageAttributes.TryGetValue("traceparent", out var traceParentAttr)
            && !string.IsNullOrWhiteSpace(traceParentAttr.StringValue))
        {
            var traceStateValue = message.MessageAttributes.TryGetValue("tracestate", out var traceStateAttr)
                ? traceStateAttr.StringValue
                : null;

            hasValidParent = ActivityContext.TryParse(traceParentAttr.StringValue, traceStateValue, out parentContext);
        }

        var activity = hasValidParent
            ? WorkOrderActivitySource.Instance.StartActivity(
                "IncidentCreated.v1 process", ActivityKind.Consumer, parentContext)
            : WorkOrderActivitySource.Instance.StartActivity(
                "IncidentCreated.v1 process", ActivityKind.Consumer);

        if (activity is null)
            return null;

        // Safe, low-cardinality messaging metadata only — never the message body.
        activity.SetTag("messaging.system", "aws_sqs");
        activity.SetTag("messaging.operation", "process");
        activity.SetTag("messaging.destination.name", "incident-created");
        activity.SetTag("messaging.message.id", message.MessageId);
        activity.SetTag("messaging.redelivery_count", receiveCount);

        return activity;
    }
}
