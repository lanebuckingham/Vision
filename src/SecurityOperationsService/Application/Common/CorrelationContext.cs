namespace Vision.SecurityOperationsService.Application.Common;

/// <summary>
/// Provides access to the current request's correlation ID.
/// Scoped per request — populated by CorrelationMiddleware.
/// </summary>
public sealed class CorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
}
