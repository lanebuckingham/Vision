using Microsoft.Extensions.Logging;

namespace Vision.WorkOrderService.Tests.Messaging;

/// <summary>
/// Test-only ILoggerProvider that records every value passed to ILogger.BeginScope
/// across the application. Used to prove that IncidentCreatedMessageProcessor
/// actually establishes a logging scope containing the event's Vision CorrelationId
/// while handling a message — not just that processing succeeds — without depending
/// on any specific logging framework's internals.
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
