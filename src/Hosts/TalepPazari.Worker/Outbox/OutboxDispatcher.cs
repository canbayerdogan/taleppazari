using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.Worker.Outbox;

// Claims due outbox messages from every module, delivers them to in-process handlers and records
// the result. Delivery is at-least-once; idempotency is the consuming module's inbox decorator.
// One pass is RunOnceAsync; the hosted WorkerService repeats it.
public sealed partial class OutboxDispatcher
{
    // Same defaults the modules serialize with (System.Text.Json web defaults).
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly OutboxDispatcherOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<OutboxDispatcher> logger;
    private readonly Dictionary<string, Type> eventTypes;
    private readonly ConcurrentDictionary<Type, EventInvoker> invokers = new();

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IIntegrationEventTypes> eventTypeRegistries,
        IOptions<OutboxDispatcherOptions> options,
        TimeProvider timeProvider,
        ILogger<OutboxDispatcher> logger)
    {
        this.scopeFactory = scopeFactory;
        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;

        // Stored type names are resolved only through the registries, never by loading a type by name.
        eventTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in eventTypeRegistries.SelectMany(registry => registry.Types))
        {
            if (!eventTypes.TryAdd(type.FullName!, type) && eventTypes[type.FullName!] != type)
            {
                throw new InvalidOperationException($"Two integration event types share the name {type.FullName}.");
            }
        }
    }

    // One pass over every module's outbox. A module whose outbox fails is logged and counted, and the
    // other modules still run. Cancellation stops the pass after the current message.
    public async Task<OutboxDispatchResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        int storeCount;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            storeCount = scope.ServiceProvider.GetServices<IOutboxStore>().Count();
        }

        var claimed = 0;
        var failedStores = 0;
        for (var index = 0; index < storeCount && !cancellationToken.IsCancellationRequested; index++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetServices<IOutboxStore>().ElementAt(index);
            try
            {
                claimed += await RunStoreAsync(store, cancellationToken);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                failedStores++;
                LogStoreFailed(logger, exception, store.ModuleName);
            }
        }

        return new OutboxDispatchResult(claimed, failedStores);
    }

    private async Task<int> RunStoreAsync(IOutboxStore store, CancellationToken cancellationToken)
    {
        var claimedAt = timeProvider.GetUtcNow();
        var claimToken = Guid.CreateVersion7(claimedAt);
        var messages = await store.ClaimAsync(
            claimToken, options.BatchSize, claimedAt, claimedAt + options.LeaseDuration, cancellationToken);

        foreach (var message in messages)
        {
            // Messages left in the batch keep their lease and are claimed again after it expires.
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await DispatchAsync(store, claimToken, message, cancellationToken);
        }

        var elapsed = timeProvider.GetUtcNow() - claimedAt;
        if (elapsed > options.LeaseDuration)
        {
            LogLeaseExceeded(logger, store.ModuleName, messages.Count, elapsed, options.LeaseDuration);
        }

        return messages.Count;
    }

    private async Task DispatchAsync(IOutboxStore store, Guid claimToken, OutboxMessage message, CancellationToken cancellationToken)
    {
        using var activity = OutboxTelemetry.ActivitySource.StartActivity(
            "outbox.dispatch", ActivityKind.Consumer, message.TraceParent);
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"] = message.Id,
            ["MessageType"] = message.Type,
            ["TraceParent"] = message.TraceParent,
            ["CorrelationId"] = message.CorrelationId,
        });
        var tags = new TagList { { "module", store.ModuleName } };

        // Attempts is counted at claim time, so a value above the limit means earlier claims never
        // finished (the process stopped or crashed while handling this message).
        if (message.Attempts > options.MaxAttempts)
        {
            await DeadLetterAsync(store, claimToken, message, tags,
                $"Claimed {message.Attempts} times without completing; MaxAttempts is {options.MaxAttempts}.");
            return;
        }

        if (!eventTypes.TryGetValue(message.Type, out var eventType))
        {
            await DeadLetterAsync(store, claimToken, message, tags,
                $"Unknown integration event type {message.Type}: no module registers it.");
            return;
        }

        object integrationEvent;
        try
        {
            integrationEvent = JsonSerializer.Deserialize(message.Payload, eventType, SerializerOptions)
                ?? throw new JsonException("The payload is null.");
        }
        catch (JsonException exception)
        {
            // The payload is not logged or stored; only the failure kind.
            await DeadLetterAsync(store, claimToken, message, tags,
                $"Payload could not be deserialized as {eventType.FullName} ({exception.GetType().FullName}).");
            return;
        }

        var invoker = invokers.GetOrAdd(eventType, CreateInvoker);
        try
        {
            int handlerCount;
            await using (var scope = CreateHandlerScope(message))
            {
                handlerCount = invoker.CountHandlers(scope.ServiceProvider);
            }

            if (handlerCount == 0)
            {
                LogNoHandlers(logger);
            }

            // One scope per handler: a failing handler cannot leave state behind for the next one.
            for (var index = 0; index < handlerCount; index++)
            {
                await using var scope = CreateHandlerScope(message);
                await invoker.InvokeAsync(scope.ServiceProvider, index, integrationEvent, cancellationToken);
            }
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            await FailAsync(store, claimToken, message, tags, exception);
            return;
        }

        if (await RecordAsync(token => store.MarkProcessedAsync(message.Id, claimToken, timeProvider.GetUtcNow(), token)))
        {
            OutboxTelemetry.Dispatched.Add(1, tags);
        }
    }

    private async Task FailAsync(IOutboxStore store, Guid claimToken, OutboxMessage message, TagList tags, Exception exception)
    {
        // Exception type and message only; the payload is never stored or logged.
        var error = $"{exception.GetType().FullName}: {exception.Message}";
        if (message.Attempts >= options.MaxAttempts)
        {
            LogHandlerFailed(logger, exception, message.Attempts, options.MaxAttempts);
            await DeadLetterAsync(store, claimToken, message, tags, error);
            return;
        }

        var nextAttemptAt = timeProvider.GetUtcNow() + RetryDelay(message.Attempts);
        LogHandlerFailed(logger, exception, message.Attempts, options.MaxAttempts);
        if (await RecordAsync(token => store.MarkFailedAsync(message.Id, claimToken, error, nextAttemptAt, token)))
        {
            OutboxTelemetry.Failed.Add(1, tags);
        }
    }

    private async Task DeadLetterAsync(IOutboxStore store, Guid claimToken, OutboxMessage message, TagList tags, string reason)
    {
        if (await RecordAsync(token => store.MarkDeadLetteredAsync(message.Id, claimToken, reason, timeProvider.GetUtcNow(), token)))
        {
            LogDeadLettered(logger, message.Attempts, reason);
            OutboxTelemetry.DeadLettered.Add(1, tags);
        }
    }

    // Results are written with their own short timeout instead of the stopping token: a shutdown must not
    // drop the result of handler work that already committed. A lost lease is logged, not an error.
    private async Task<bool> RecordAsync(Func<CancellationToken, Task<bool>> write)
    {
        using var timeout = new CancellationTokenSource(options.AcknowledgeTimeout);
        if (await write(timeout.Token))
        {
            return true;
        }

        LogLeaseLost(logger);
        return false;
    }

    private TimeSpan RetryDelay(int attempt)
    {
        var factor = Math.Pow(2, Math.Min(attempt - 1, 30));
        var delay = options.RetryBaseDelay.TotalMilliseconds * factor;
        return delay >= options.RetryMaxDelay.TotalMilliseconds ? options.RetryMaxDelay : TimeSpan.FromMilliseconds(delay);
    }

    private AsyncServiceScope CreateHandlerScope(OutboxMessage message)
    {
        var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IntegrationEventContext>().Set(message);
        return scope;
    }

    private static EventInvoker CreateInvoker(Type eventType) =>
        (EventInvoker)Activator.CreateInstance(typeof(EventInvoker<>).MakeGenericType(eventType))!;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox pass for module {Module} failed; retrying after back-off")]
    private static partial void LogStoreFailed(ILogger logger, Exception exception, string module);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handling outbox message failed (attempt {Attempt} of {MaxAttempts})")]
    private static partial void LogHandlerFailed(ILogger logger, Exception exception, int attempt, int maxAttempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message dead-lettered after {Attempts} attempts: {Reason}")]
    private static partial void LogDeadLettered(ILogger logger, int attempts, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lease lost: another dispatcher owns the outbox message now, so this result was not recorded")]
    private static partial void LogLeaseLost(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No handler is registered for the outbox message; marking it processed")]
    private static partial void LogNoHandlers(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox batch of {Count} messages for module {Module} took {Elapsed}, longer than its lease {LeaseDuration}; lower Outbox:BatchSize or raise Outbox:LeaseDuration")]
    private static partial void LogLeaseExceeded(ILogger logger, string module, int count, TimeSpan elapsed, TimeSpan leaseDuration);

    private abstract class EventInvoker
    {
        public abstract int CountHandlers(IServiceProvider services);

        public abstract Task InvokeAsync(IServiceProvider services, int index, object integrationEvent, CancellationToken cancellationToken);
    }

    private sealed class EventInvoker<TEvent> : EventInvoker
        where TEvent : class
    {
        public override int CountHandlers(IServiceProvider services) =>
            services.GetServices<IIntegrationEventHandler<TEvent>>().Count();

        public override Task InvokeAsync(IServiceProvider services, int index, object integrationEvent, CancellationToken cancellationToken) =>
            services.GetServices<IIntegrationEventHandler<TEvent>>().ElementAt(index).HandleAsync((TEvent)integrationEvent, cancellationToken);
    }
}
