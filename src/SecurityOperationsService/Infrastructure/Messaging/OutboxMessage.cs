namespace Vision.SecurityOperationsService.Infrastructure.Messaging;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public required string EventType { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public required string CorrelationId { get; set; }

    /// <summary>
    /// W3C trace context of the HTTP request that created this outbox row, captured so
    /// the distributed trace can be resumed when the background publisher later sends
    /// this message to SQS. Null when no current Activity existed at creation time (for
    /// example a maintenance/test path) — this is expected and must not block publication.
    /// Observability metadata only; not a business identifier. See CorrelationId for the
    /// durable, business-level identifier used for logs/DB investigation.
    /// </summary>
    public string? TraceParent { get; set; }
    public string? TraceState { get; set; }
}
