using System.Diagnostics;

namespace Vision.WorkOrderService.Infrastructure.Messaging;

/// <summary>
/// Custom ActivitySource for the SQS consumer boundary, where automatic ASP.NET Core
/// instrumentation cannot help — message processing happens on a background polling
/// loop, not an inbound HTTP request. Registered with OpenTelemetry via
/// AddSource(WorkOrderActivitySource.Name) in VisionObservabilityExtensions.
/// </summary>
public static class WorkOrderActivitySource
{
    public const string Name = "Vision.WorkOrderService";

    public static readonly ActivitySource Instance = new(Name);
}
