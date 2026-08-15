using Vision.SecurityOperationsService.Application.Common;

namespace Vision.SecurityOperationsService.API.Middleware;

/// <summary>
/// Extracts or generates a correlation ID per request.
/// Uses X-Correlation-ID header if present; otherwise generates one.
/// Exposes the value through scoped CorrelationContext and response header.
/// </summary>
public sealed class CorrelationMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, CorrelationContext correlationContext)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        // Treat incoming value as untrusted: reject blank, overlength, or unsafe content
        if (string.IsNullOrWhiteSpace(correlationId)
            || correlationId.Length > 100
            || correlationId.AsSpan().IndexOfAny('\r', '\n') >= 0)
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        correlationContext.CorrelationId = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await next(context);
    }
}
