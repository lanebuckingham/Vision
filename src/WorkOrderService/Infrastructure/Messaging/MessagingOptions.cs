namespace Vision.WorkOrderService.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public IncidentCreatedQueueOptions IncidentCreated { get; set; } = new();
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
