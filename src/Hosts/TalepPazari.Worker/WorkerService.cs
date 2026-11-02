using Microsoft.Extensions.Options;
using TalepPazari.Worker.Outbox;

namespace TalepPazari.Worker;

// Logs once, then repeats outbox dispatcher passes until shutdown. A failing pass (for example,
// database down) never stops the loop: it backs off exponentially up to Outbox:ErrorBackoffMax.
internal sealed partial class WorkerService(
    OutboxDispatcher dispatcher,
    IOptions<OutboxDispatcherOptions> options,
    TimeProvider timeProvider,
    ILogger<WorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);

        var settings = options.Value;
        var errorDelay = settings.PollInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                var result = await dispatcher.RunOnceAsync(stoppingToken);
                if (result.FailedStores > 0)
                {
                    delay = errorDelay;
                    errorDelay = TimeSpan.FromTicks(Math.Min(errorDelay.Ticks * 2, settings.ErrorBackoffMax.Ticks));
                }
                else
                {
                    errorDelay = settings.PollInterval;
                    delay = result.Claimed == 0 ? settings.PollInterval : TimeSpan.Zero;
                }

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, timeProvider, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker started")]
    private static partial void LogStarted(ILogger logger);
}
