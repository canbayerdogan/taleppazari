namespace TalepPazari.Worker;

// Skeleton only: logs once and idles until shutdown. Real jobs arrive with later tasks.
internal sealed partial class WorkerService(ILogger<WorkerService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);
        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker started")]
    private static partial void LogStarted(ILogger logger);
}
