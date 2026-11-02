using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using TalepPazari.Modules.Example.Contracts.IntegrationEvents;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.IntegrationTests.Support;

// Test-only handlers, registered without the inbox decorator so they see every delivery.

public sealed class ThrowingHandler : IIntegrationEventHandler<ExampleItemCreatedV1>
{
    public Task HandleAsync(ExampleItemCreatedV1 integrationEvent, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Simulated handler failure.");
}

// Records every delivery (message id) in a log shared by all hosts of one test.
public sealed class RecordingHandler(DeliveryLog log, IIntegrationEventContext context) : IIntegrationEventHandler<ExampleItemCreatedV1>
{
    public async Task HandleAsync(ExampleItemCreatedV1 integrationEvent, CancellationToken cancellationToken)
    {
        log.Deliveries.Add(context.MessageId);
        await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
    }
}

public sealed class DeliveryLog
{
    public ConcurrentBag<Guid> Deliveries { get; } = [];
}

// Cancels the dispatcher's token while the message is being handled, as a host shutdown would.
public sealed class CancellingHandler(ShutdownSignal signal) : IIntegrationEventHandler<ExampleItemCreatedV1>
{
    public Task HandleAsync(ExampleItemCreatedV1 integrationEvent, CancellationToken cancellationToken)
    {
        signal.Source.Cancel();
        return Task.CompletedTask;
    }
}

public sealed class ShutdownSignal : IDisposable
{
    public CancellationTokenSource Source { get; } = new();

    public void Dispose() => Source.Dispose();
}

// Simulates a process that dies after its handlers committed but before the acknowledgement was written.
public sealed class CrashBeforeAcknowledgementStore(IOutboxStore inner) : IOutboxStore
{
    public string ModuleName => inner.ModuleName;

    public Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        Guid claimToken, int batchSize, DateTimeOffset now, DateTimeOffset lockedUntil, CancellationToken cancellationToken) =>
        inner.ClaimAsync(claimToken, batchSize, now, lockedUntil, cancellationToken);

    public Task<bool> MarkProcessedAsync(Guid messageId, Guid claimToken, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new IOException("Simulated crash before the acknowledgement.");

    public Task<bool> MarkFailedAsync(
        Guid messageId, Guid claimToken, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken) =>
        inner.MarkFailedAsync(messageId, claimToken, error, nextAttemptAt, cancellationToken);

    public Task<bool> MarkDeadLetteredAsync(
        Guid messageId, Guid claimToken, string error, DateTimeOffset now, CancellationToken cancellationToken) =>
        inner.MarkDeadLetteredAsync(messageId, claimToken, error, now, cancellationToken);

    // Replaces the module's store registration with this wrapper around it.
    public static void Install(IServiceCollection services)
    {
        var original = services.Single(descriptor => descriptor.ServiceType == typeof(IOutboxStore));
        services.Remove(original);
        services.AddScoped<IOutboxStore>(provider =>
            new CrashBeforeAcknowledgementStore((IOutboxStore)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!)));
    }
}

// An event type that is registered but has no handler.
public sealed record UnhandledTestEventV1(Guid Id);

public sealed class TestEventTypes : IIntegrationEventTypes
{
    public IReadOnlyCollection<Type> Types { get; } = [typeof(UnhandledTestEventV1)];
}
