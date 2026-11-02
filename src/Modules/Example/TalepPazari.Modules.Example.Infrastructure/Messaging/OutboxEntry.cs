namespace TalepPazari.Modules.Example.Infrastructure.Messaging;

// One row of example."OutboxMessages". Written by ExampleOutbox, claimed and updated by ExampleOutboxStore.
internal sealed class OutboxEntry
{
    public const int LastErrorMaxLength = 2000;

    public Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Payload { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public string? TraceParent { get; init; }

    public string? CorrelationId { get; init; }

    public int Attempts { get; init; }

    public DateTimeOffset NextAttemptAt { get; init; }

    public DateTimeOffset? LockedUntil { get; init; }

    public Guid? LockedBy { get; init; }

    public DateTimeOffset? ProcessedAt { get; init; }

    public DateTimeOffset? DeadLetteredAt { get; init; }

    public string? LastError { get; init; }
}
