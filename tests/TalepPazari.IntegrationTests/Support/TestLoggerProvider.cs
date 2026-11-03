using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TalepPazari.IntegrationTests.Support;

// Captures every log entry (all levels) of one test host, so tests can check what reaches the logs.
public sealed class TestLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, Entries);

    public void Dispose()
    {
    }

    private sealed class TestLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
    }
}

public sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);
