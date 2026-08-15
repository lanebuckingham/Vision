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
            return false;
        }

        if (evt is null)
        {
            logger.LogWarning("Deserialized null event from SQS message {MessageId} (receive #{ReceiveCount})",
                message.MessageId, receiveCount);
            return false;
        }

        if (evt.EventType != IncidentCreatedV1.EventTypeName)
        {
            logger.LogWarning(
                "Unsupported event type '{EventType}' in message {MessageId} (receive #{ReceiveCount}). Expected {Expected}",
                evt.EventType, message.MessageId, receiveCount, IncidentCreatedV1.EventTypeName);
            return false;
        }

        logger.LogInformation(
            "Received IncidentCreated event {EventId} for incident {IncidentId} (receive #{ReceiveCount})",
            evt.EventId, evt.Incident?.Id, receiveCount);

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

            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process event {EventId} from message {MessageId} (receive #{ReceiveCount})",
                evt.EventId, message.MessageId, receiveCount);
            return false;
        }
    }
}
