using TalepPazari.SharedKernel.Observability;

namespace TalepPazari.Api.Observability;

// Scoped: one per request. CorrelationIdMiddleware sets it before the rest of the pipeline runs, so every
// service resolved from the request scope (for example a module's outbox) sees the request's value.
// Work started in a new DI scope inside the request does not see it.
internal sealed class CorrelationContext : ICorrelationContext
{
    public string? CorrelationId { get; private set; }

    public void Set(string correlationId) => CorrelationId = correlationId;
}
