using TalepPazari.SharedKernel.Messaging;
using TalepPazari.SharedKernel.Observability;

namespace TalepPazari.Worker.Outbox;

// Scoped: the dispatcher sets it in each handler's scope before resolving the handler.
// It is also the scope's ICorrelationContext: the message's correlation ID is restored for the handler,
// so events the handler publishes carry it. Outside a handler scope it is unset (null).
internal sealed class IntegrationEventContext : IIntegrationEventContext, ICorrelationContext
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
