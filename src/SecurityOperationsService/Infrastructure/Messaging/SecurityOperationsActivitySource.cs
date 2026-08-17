using System.Diagnostics;

namespace Vision.SecurityOperationsService.Infrastructure.Messaging;

/// <summary>
/// Custom ActivitySource for spans that automatic instrumentation cannot produce —
/// specifically the transactional-outbox publish boundary, where the SQS send happens
/// on a background thread well after the originating HTTP request may have completed.
/// Registered with OpenTelemetry via AddSource(SecurityOperationsActivitySource.Name)
/// in VisionObservabilityExtensions.
/// </summary>
public static class SecurityOperationsActivitySource
{
    public const string Name = "Vision.SecurityOperationsService";

    public static readonly ActivitySource Instance = new(Name);
}
