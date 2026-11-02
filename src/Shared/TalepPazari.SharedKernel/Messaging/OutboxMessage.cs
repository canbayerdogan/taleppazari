namespace TalepPazari.SharedKernel.Messaging;

// A claimed outbox row, as handed from a module's IOutboxStore to the dispatcher. Data only.
// Type: the event's CLR full name (no assembly name or version), resolved only through IIntegrationEventTypes.
// Attempts: claims so far, including the current one (incremented when the row is claimed).
// CorrelationId: a hook only; written as null until T-008 defines generation and validation.
public sealed record OutboxMessage(
    Guid Id,
    string Type,
    string Payload,
    DateTimeOffset OccurredAt,
    int Attempts,
    string? TraceParent,
    string? CorrelationId);
