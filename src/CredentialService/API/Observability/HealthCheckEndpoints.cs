using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Vision.CredentialService.API.Observability;

/// <summary>
/// Maps /health/live and /health/ready with distinct semantics:
///   /health/live  — is the process alive? Never touches PostgreSQL. A temporary
///                   dependency outage must not make the app look dead and trigger a
///                   restart loop.
///   /health/ready — is the service ready to do its work? Includes PostgreSQL
///                   connectivity for this service's own schema.
/// /health is retained as a compatibility alias for local developer tooling.
/// </summary>
public static class HealthCheckEndpoints
{
    public static WebApplication MapVisionHealthEndpoints(this WebApplication app, string serviceName)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteResponse
        }).WithName("HealthLive");

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteResponse
        }).WithName("HealthReady");

        app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = serviceName }))
            .WithName("HealthCheck");

        return app;
    }

    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
        };

        return context.Response.WriteAsJsonAsync(payload);
    }
}
