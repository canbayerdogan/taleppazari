using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.Worker.Outbox;

// Scoped: the dispatcher sets it in each handler's scope before resolving the handler.
internal sealed class IntegrationEventContext : IIntegrationEventContext
{
    public Guid MessageId { get; private set; }

    public string? TraceParent { get; private set; }

    public string? CorrelationId { get; private set; }

    public void Set(OutboxMessage message)
    {
        MessageId = message.Id;
        TraceParent = message.TraceParent;
        CorrelationId = message.CorrelationId;
    }
}
