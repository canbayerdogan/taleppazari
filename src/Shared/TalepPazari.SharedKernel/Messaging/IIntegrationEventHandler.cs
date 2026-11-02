namespace TalepPazari.SharedKernel.Messaging;

// Handles one integration event type. Delivery is at-least-once; the consuming module wraps every
// handler in its inbox decorator so a redelivered message has no second effect.
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
