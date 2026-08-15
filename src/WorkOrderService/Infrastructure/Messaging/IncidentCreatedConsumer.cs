using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;

namespace Vision.WorkOrderService.Infrastructure.Messaging;

/// <summary>
/// Background service that long-polls the incident-created SQS queue and processes messages.
/// Delegates actual message processing to IncidentCreatedMessageProcessor for testability.
/// </summary>
public sealed class IncidentCreatedConsumer(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqsClient,
    IOptions<MessagingOptions> options,
    ILogger<IncidentCreatedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var messaging = options.Value;
        var queueName = messaging.IncidentCreated.QueueName;
        var waitTimeSeconds = messaging.IncidentCreated.WaitTimeSeconds;
        var maxMessages = messaging.IncidentCreated.MaxNumberOfMessages;
        var visibilityTimeout = messaging.IncidentCreated.VisibilityTimeoutSeconds;

        if (string.IsNullOrWhiteSpace(queueName))
        {
            logger.LogWarning("SQS consumer disabled: Messaging:IncidentCreated:QueueName not configured");
            return;
        }

        // Resolve queue URL from name — retry until available
        string? queueUrl = null;
        while (!stoppingToken.IsCancellationRequested && queueUrl is null)
        {
            try
            {
                var response = await sqsClient.GetQueueUrlAsync(queueName, stoppingToken);
                queueUrl = response.QueueUrl;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to resolve queue URL for '{QueueName}', retrying in 5s...", queueName);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation(
            "SQS consumer started. Queue: {QueueUrl}, WaitTime: {WaitTime}s, MaxMessages: {MaxMessages}",
            queueUrl, waitTimeSeconds, maxMessages);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var receiveRequest = new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    WaitTimeSeconds = waitTimeSeconds,
                    MaxNumberOfMessages = maxMessages,
                    VisibilityTimeout = visibilityTimeout,
                    MessageAttributeNames = ["All"],
                    MessageSystemAttributeNames = ["ApproximateReceiveCount"]
                };

                var response = await sqsClient.ReceiveMessageAsync(receiveRequest, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IncidentCreatedMessageProcessor>();

                foreach (var message in response.Messages)
                {
                    await processor.ProcessAsync(message, queueUrl!, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SQS consumer encountered an error during receive");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("SQS consumer stopped");
    }
}
