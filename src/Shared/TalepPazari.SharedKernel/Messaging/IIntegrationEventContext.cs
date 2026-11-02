namespace TalepPazari.SharedKernel.Messaging;

// The message being delivered. Set by the dispatcher for each handler invocation (one DI scope each);
// read by the consuming module's inbox decorator.
public interface IIntegrationEventContext
{
    Guid MessageId { get; }

    string? TraceParent { get; }

    string? CorrelationId { get; }
}
