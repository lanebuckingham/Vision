using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Vision.SecurityOperationsService.API.Middleware;
using Vision.SecurityOperationsService.Application.Common;

namespace Vision.SecurityOperationsService.Tests.Application;

/// <summary>
/// Proves that CorrelationMiddleware establishes an ILogger.BeginScope containing the
/// durable Vision CorrelationId around downstream request processing, so any log line
/// emitted while handling the request can be searched by CorrelationId. Invokes the
/// middleware directly (bypassing the full HTTP pipeline) against a recording ILogger
/// to avoid brittle assertions on logging-framework internals.
/// </summary>
public class CorrelationLoggingScopeTests
{
    [Fact]
    public async Task InvokeAsync_EstablishesLoggingScopeContainingCorrelationId()
    {
        var recordingLogger = new RecordingLogger<CorrelationMiddleware>();
        object? scopeDuringDownstreamProcessing = null;

        RequestDelegate next = ctx =>
        {
            // Capture the active scope at the moment downstream processing runs — the
            // scope is disposed (and removed) once InvokeAsync's using block exits, so
            // it must be observed from inside next(), not after InvokeAsync returns.
            scopeDuringDownstreamProcessing = recordingLogger.CapturedScopes.LastOrDefault();
            return Task.CompletedTask;
        };

        var middleware = new CorrelationMiddleware(next, recordingLogger);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "test-scope-correlation-id";

        await middleware.InvokeAsync(context, new CorrelationContext());

        Assert.NotNull(scopeDuringDownstreamProcessing);
        var scopeDictionary = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(scopeDuringDownstreamProcessing);
        Assert.Equal("test-scope-correlation-id", scopeDictionary["CorrelationId"]);

        // The scope must be cleaned up once request processing completes.
        Assert.Empty(recordingLogger.CapturedScopes);
    }

    [Fact]
    public async Task InvokeAsync_ScopeCorrelationId_MatchesGeneratedValueWhenHeaderAbsent()
    {
        var recordingLogger = new RecordingLogger<CorrelationMiddleware>();
        string? correlationIdDuringScope = null;

        RequestDelegate next = ctx =>
        {
            // Must observe the scope from inside next() — it is popped once InvokeAsync's
            // using block completes.
            var scope = recordingLogger.CapturedScopes.LastOrDefault() as IReadOnlyDictionary<string, object>;
            correlationIdDuringScope = scope?["CorrelationId"] as string;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationMiddleware(next, recordingLogger);
        var context = new DefaultHttpContext();
        var correlationContext = new CorrelationContext();

        await middleware.InvokeAsync(context, correlationContext);

        Assert.False(string.IsNullOrWhiteSpace(correlationIdDuringScope));
        // The scope value must be the same value ultimately stored on CorrelationContext
        // and returned in the response header — not an independently generated one.
        Assert.Equal(correlationContext.CorrelationId, correlationIdDuringScope);
    }

    /// <summary>
    /// Minimal ILogger test double that records BeginScope state objects so tests can
    /// assert scope contents without depending on a real logging provider's internals.
    /// </summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<object> CapturedScopes { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            CapturedScopes.Add(state);
            return new PopOnDispose(this, state);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // No-op: this double only needs to capture scopes for these tests.
        }

        private sealed class PopOnDispose(RecordingLogger<T> owner, object state) : IDisposable
        {
            public void Dispose() => owner.CapturedScopes.Remove(state);
        }
    }
}
