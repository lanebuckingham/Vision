namespace Vision.SecurityOperationsService.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public IncidentCreatedQueueOptions IncidentCreated { get; set; } = new();
    public OutboxOptions Outbox { get; set; } = new();
}

public sealed class IncidentCreatedQueueOptions
{
    public string QueueName { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string? ServiceUrl { get; set; }
    public int WaitTimeSeconds { get; set; } = 20;
    public int VisibilityTimeoutSeconds { get; set; } = 60;
    public int MaxNumberOfMessages { get; set; } = 10;
}

public sealed class OutboxOptions
{
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
}
