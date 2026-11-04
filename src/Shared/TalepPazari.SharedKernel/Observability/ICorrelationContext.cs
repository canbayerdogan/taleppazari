namespace TalepPazari.SharedKernel.Observability;

// The correlation ID of the operation the current DI scope serves (ADR-007), or null when there is none.
// An event-propagation abstraction (ADR-002, ADR-007): hosts implement it per scope (the Api per request,
// the Worker per handled outbox message); modules only read it, for example to stamp outbox rows.
// Modules must treat it as optional: a composition without a host implementation has no correlation ID.
// The value is validated by the host, is not a secret and is never used for authorization.
public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
