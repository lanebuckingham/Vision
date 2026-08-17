using System.Diagnostics;
using Microsoft.Extensions.Logging.Console;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Vision.SecurityOperationsService.Infrastructure.Messaging;

namespace Vision.SecurityOperationsService.API.Observability;

/// <summary>
/// Registers OpenTelemetry tracing and trace-correlated logging for
/// SecurityOperationsService. Mirrors the equivalent helper in WorkOrderService and
/// CredentialService — kept per-service rather than in a shared project per the
/// architecture rule against introducing shared-platform infrastructure for the MVP.
/// </summary>
public static class VisionObservabilityExtensions
{
    public const string ServiceName = "vision-security-operations-service";

    public static WebApplicationBuilder AddVisionObservability(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var environment = builder.Environment;

        var options = new ObservabilityOptions();
        configuration.GetSection(ObservabilityOptions.SectionName).Bind(options);
        ValidateSamplingRatio(options.SamplingRatio);

        // Trace/span IDs in log scopes regardless of whether OpenTelemetry providers
        // are registered below — this is cheap, standard, and useful even with
        // Observability:Enabled=false.
        builder.Logging.Configure(o =>
        {
            o.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.ParentId;
        });

        // Container-friendly stdout logging: structured JSON outside Development,
        // human-readable single-line console during local development.
        if (environment.IsDevelopment())
        {
            builder.Logging.AddSimpleConsole(o =>
            {
                o.IncludeScopes = true;
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
        }
        else
        {
            builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
        }

        if (!options.Enabled)
            return builder;

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(ServiceName, serviceVersion: GetServiceVersion())
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment.name", environment.EnvironmentName)
            });

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SamplingRatio)))
                    .AddSource(SecurityOperationsActivitySource.Name)
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        // Routine liveness/readiness polling should not dominate exported traces.
                        o.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health");
                        o.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(o => o.RecordException = true);

                // Traces PostgreSQL commands at the Npgsql/ADO.NET boundary, covering every
                // command EF Core issues without depending on the still-beta
                // OpenTelemetry.Instrumentation.EntityFrameworkCore package. Fully qualified
                // because Npgsql.EntityFrameworkCore.PostgreSQL also ships an AddNpgsql
                // extension (for IServiceCollection) that otherwise wins overload resolution.
                Npgsql.TracerProviderBuilderExtensions.AddNpgsql(tracing);

                if (options.ConsoleExporterEnabled)
                    tracing.AddConsoleExporter();

                // OTLP endpoint/headers are read from standard OTEL_EXPORTER_OTLP_* environment
                // variables when not explicitly configured here. Exporter unavailability fails
                // in the background (batch export retries/logs) and never blocks the app.
                tracing.AddOtlpExporter();
            });

        return builder;
    }

    private static string GetServiceVersion()
    {
        return typeof(VisionObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Fails fast with a clear, actionable configuration error rather than letting an
    /// out-of-range value reach TraceIdRatioBasedSampler, which throws an
    /// ArgumentOutOfRangeException with a much less obvious call stack.
    /// </summary>
    private static void ValidateSamplingRatio(double samplingRatio)
    {
        if (samplingRatio < 0.0 || samplingRatio > 1.0)
        {
            throw new InvalidOperationException(
                $"Observability:SamplingRatio must be between 0.0 and 1.0 (inclusive). Configured value: {samplingRatio}.");
        }
    }
}
