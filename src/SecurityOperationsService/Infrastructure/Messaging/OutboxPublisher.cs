using Amazon.SQS;
using Microsoft.Extensions.Options;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Infrastructure.Messaging;

/// <summary>
/// Background service that polls unpublished outbox messages and sends them to SQS.
/// Delegates actual batch processing to OutboxBatchProcessor for testability.
/// </summary>
public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqsClient,
    IOptions<MessagingOptions> options,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var messaging = options.Value;
        var queueName = messaging.IncidentCreated.QueueName;
        var pollInterval = TimeSpan.FromSeconds(messaging.Outbox.PollIntervalSeconds);
        var batchSize = messaging.Outbox.BatchSize;

        if (string.IsNullOrWhiteSpace(queueName))
        {
            logger.LogWarning("Outbox publisher disabled: Messaging:IncidentCreated:QueueName not configured");
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

        logger.LogInformation("Outbox publisher started. Queue: {QueueUrl}, Poll interval: {Interval}s",
            queueUrl, messaging.Outbox.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>();

                var published = await processor.PublishBatchAsync(db, queueUrl!, batchSize, stoppingToken);

                // If we published a full batch, immediately check for more
                if (published >= batchSize)
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publisher encountered an error");
            }

            await Task.Delay(pollInterval, stoppingToken);
        }

        logger.LogInformation("Outbox publisher stopped");
    }
}
