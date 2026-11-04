using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TalepPazari.IntegrationTests.Support;

// Captures every log entry (all levels) of one test host, so tests can check what reaches the logs.
// It also records the scope values active when the entry was written (for example CorrelationId), as the
// JSON console formatter would print them with IncludeScopes.
public sealed class TestLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, Entries, () => scopeProvider);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => this.scopeProvider = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class TestLogger(string category, ConcurrentQueue<LogEntry> entries, Func<IExternalScopeProvider> scopes) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => scopes().Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = new List<KeyValuePair<string, object?>>();
            scopes().ForEachScope(
                (scope, list) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        list.AddRange(pairs);
                    }
                    else if (scope is not null)
                    {
                        list.Add(new("(scope)", scope.ToString()));
                    }
                },
                values);
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception, values));
        }
    }
}

public sealed record LogEntry(
    string Category, LogLevel Level, string Message, Exception? Exception, IReadOnlyList<KeyValuePair<string, object?>> Scopes)
{
    // Everything an entry could expose: formatted message, scope values and the full exception text.
    public string AllText =>
        string.Join('\n', [Message, .. Scopes.Select(pair => $"{pair.Key}={pair.Value}"), Exception?.ToString() ?? string.Empty]);

    public object? Scope(string key) => Scopes.LastOrDefault(pair => pair.Key == key).Value;
}
