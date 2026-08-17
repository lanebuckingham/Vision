using Microsoft.Extensions.Logging;

namespace Vision.SecurityOperationsService.Tests.Application;

/// <summary>
/// Test-only ILoggerProvider that records every value passed to ILogger.BeginScope
/// across the application. Used to prove that CorrelationMiddleware actually
/// establishes a logging scope containing CorrelationId — not just that requests
/// succeed — without depending on any specific logging framework's internals.
/// </summary>
public sealed class ScopeCapturingLoggerProvider : ILoggerProvider
{
    public List<object?> CapturedScopes { get; } = [];

    public ILogger CreateLogger(string categoryName) => new ScopeCapturingLogger(this);

    public void Dispose() { }

    private sealed class ScopeCapturingLogger(ScopeCapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            lock (provider.CapturedScopes)
            {
                provider.CapturedScopes.Add(state);
            }
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Not needed for scope-capture assertions.
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
