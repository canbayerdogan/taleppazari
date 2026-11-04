using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using TalepPazari.Modules.Example.Infrastructure.Persistence;

namespace TalepPazari.Modules.Example.Infrastructure.Health;

// Readiness: can the Example DbContext reach its database (ADR-007: the host's own critical dependency).
// Never throws: every failure (unreachable, wrong password, missing connection string, timeout) is
// Unhealthy with a fixed description and no exception object, so neither the response nor the health
// check service's log can carry a connection string or an exception message. The full exception is
// logged at Debug. The DbContext is resolved inside the try, because building its options throws when the
// connection string is missing (that would otherwise escape the check). State changes (reachable <-> unreachable) are logged once each, so repeated probes
// against a down database do not repeat Warning entries.
internal sealed partial class ExampleDatabaseHealthCheck(
    IServiceProvider services, ExampleReadinessState state, ILogger<ExampleDatabaseHealthCheck> logger) : IHealthCheck
{
    public const string Name = "example-database";
    public const string UnhealthyDescription = "The Example database is not reachable.";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        bool reachable;
        try
        {
            var dbContext = services.GetRequiredService<ExampleDbContext>();
            reachable = await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            reachable = false;
            LogCheckFailedDetail(logger, exception);
        }

        if (state.Changed(reachable))
        {
            if (reachable)
            {
                LogReachable(logger);
            }
            else
            {
                LogUnreachable(logger);
            }
        }

        return reachable ? HealthCheckResult.Healthy() : new HealthCheckResult(context.Registration.FailureStatus, UnhealthyDescription);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Readiness: the Example database became unreachable (details at Debug)")]
    private static partial void LogUnreachable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Readiness: the Example database is reachable")]
    private static partial void LogReachable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Readiness check of the Example database failed")]
    private static partial void LogCheckFailedDetail(ILogger logger, Exception exception);
}

// Singleton: the last reported readiness, so only state changes are logged.
internal sealed class ExampleReadinessState
{
    private const int Unknown = -1;
    private int last = Unknown;

    public bool Changed(bool reachable)
    {
        var current = reachable ? 1 : 0;
        return Interlocked.Exchange(ref last, current) != current;
    }
}
