using System.Diagnostics;
using System.Text.Json;
using TalepPazari.Modules.Example.Infrastructure.Persistence;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.Modules.Example.Infrastructure.Messaging;

internal sealed class ExampleOutbox(ExampleDbContext dbContext, ExampleIntegrationEventTypes eventTypes, TimeProvider timeProvider)
    : IOutbox
{
    // The dispatcher deserializes with the same defaults (System.Text.Json web defaults).
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Add<TEvent>(TEvent integrationEvent)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (!eventTypes.Types.Contains(typeof(TEvent)))
        {
            throw new InvalidOperationException(
                $"{typeof(TEvent).FullName} is not registered in {nameof(ExampleIntegrationEventTypes)}; the dispatcher could not resolve it.");
        }

        var now = timeProvider.GetUtcNow();
        dbContext.OutboxMessages.Add(new OutboxEntry
        {
            Id = Guid.CreateVersion7(now),
            Type = typeof(TEvent).FullName!,
            Payload = JsonSerializer.Serialize(integrationEvent, SerializerOptions),
            OccurredAt = now,
            NextAttemptAt = now,

            // Propagated only. Correlation ID generation and validation belong to T-008.
            TraceParent = Activity.Current?.Id,
            CorrelationId = null,
        });
    }
}
