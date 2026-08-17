namespace Vision.WorkOrderService.API.Observability;

/// <summary>
/// Vision-specific observability configuration. Standard OTEL_* environment variables
/// (e.g. OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_HEADERS) are preferred for
/// exporter destination/credentials; this section covers the small set of toggles
/// that aren't already covered cleanly by those conventions.
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>Master switch. When false, no OpenTelemetry providers are registered.
    /// Application logging and health endpoints are never affected by this setting.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Opt-in local trace visibility without an external collector.</summary>
    public bool ConsoleExporterEnabled { get; set; }

    /// <summary>Fraction of root traces sampled (0.0-1.0). Ignored for child spans of an
    /// already-sampled parent (ParentBasedSampler). 1.0 is appropriate for the MVP's low
    /// traffic volume; lower in a higher-throughput deployment via configuration only.</summary>
    public double SamplingRatio { get; set; } = 1.0;
}
