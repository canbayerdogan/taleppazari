using System.Collections;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace TalepPazari.Worker.Observability;

// The project logging rule (T-007b, T-008b), enforced in code so no configuration source (appsettings,
// environment variables, provider-specific Logging:<Provider>:LogLevel rules) can loosen it:
//   1. An entry at Warning or above never carries an exception object or text built from one, whatever its
//      category (EF Core, Npgsql, Kestrel, health checks, hosting, our own code). Such an entry is split:
//      the original entry (exception included) is written at Debug, and a fixed entry naming only the event
//      and the exception type is written at the original level.
//   2. Categories listed as floors are never enabled below Warning (the Api floors Microsoft.AspNetCore,
//      whose Information and Debug entries print the URL with its query string and bound parameter values).
//   3. Categories listed as Debug-only are written at Debug at most (the Api lists the health check service,
//      which writes an Error entry for every failing probe; module checks log state changes themselves).
// It wraps the host's ILoggerFactory, so it applies to every provider, including those added later.
// One copy per host, kept identical except for the namespace (docs/architecture/observability.md).
internal static class LogPolicy
{
    public static IServiceCollection AddLogPolicy(
        this IServiceCollection services, string[]? warningFloorCategories = null, string[]? debugOnlyCategories = null)
    {
        services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(provider => new PolicyLoggerFactory(
            new LoggerFactory(
                provider.GetServices<ILoggerProvider>(),
                provider.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>(),
                provider.GetService<IOptions<LoggerFactoryOptions>>(),
                provider.GetService<IExternalScopeProvider>()),
            warningFloorCategories ?? [],
            debugOnlyCategories ?? [])));
        return services;
    }

    private static bool IsUnder(string category, string prefix) =>
        category.StartsWith(prefix, StringComparison.Ordinal)
        && (category.Length == prefix.Length || category[prefix.Length] == '.');

    private sealed class PolicyLoggerFactory(LoggerFactory inner, string[] warningFloorCategories, string[] debugOnlyCategories)
        : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName)
        {
            var minimum = warningFloorCategories.Any(prefix => IsUnder(categoryName, prefix)) ? LogLevel.Warning : LogLevel.Trace;
            var maximum = debugOnlyCategories.Any(prefix => IsUnder(categoryName, prefix)) ? LogLevel.Debug : LogLevel.Critical;
            return new PolicyLogger(inner.CreateLogger(categoryName), minimum, maximum);
        }

        public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);

        public void Dispose() => inner.Dispose();
    }

    private sealed class PolicyLogger(ILogger inner, LogLevel minimum, LogLevel maximum) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None && Capped(logLevel) >= minimum && inner.IsEnabled(Capped(logLevel));

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.None)
            {
                return;
            }

            logLevel = Capped(logLevel);
            if (logLevel < minimum)
            {
                return;
            }

            if (exception is null || logLevel < LogLevel.Warning)
            {
                inner.Log(logLevel, eventId, state, exception, formatter);
                return;
            }

            // The original text may embed the exception message (EF Core's error templates do), so it moves
            // to Debug together with the exception. The Warning+ entry keeps only fixed, data-free values.
            if (LogLevel.Debug >= minimum)
            {
                inner.Log(LogLevel.Debug, eventId, state, exception, formatter);
            }

            inner.Log(logLevel, eventId, new StrippedEntry(eventId, exception.GetType().FullName), null, StrippedEntry.Format);
        }

        private LogLevel Capped(LogLevel logLevel) => logLevel > maximum ? maximum : logLevel;
    }

    // Structured state of the Warning+ entry that replaces one carrying an exception.
    private sealed class StrippedEntry(EventId eventId, string? exceptionType) : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private const string OriginalFormat = "{EventName} failed with {ExceptionType}; message and exception are logged at Debug only";

        public static readonly Func<StrippedEntry, Exception?, string> Format = (entry, _) =>
            $"{entry.EventName} failed with {entry.ExceptionType}; message and exception are logged at Debug only";

        public string EventName { get; } = eventId.Name ?? eventId.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public string? ExceptionType { get; } = exceptionType;

        public int Count => 3;

        public KeyValuePair<string, object?> this[int index] => index switch
        {
            0 => new("EventName", EventName),
            1 => new("ExceptionType", ExceptionType),
            2 => new("{OriginalFormat}", OriginalFormat),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => Format(this, null);
    }
}
