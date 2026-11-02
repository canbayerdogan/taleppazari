namespace TalepPazari.SharedKernel.Messaging;

// The contract between the dispatcher and one module's outbox table. Each module implements it
// internally and registers it as a scoped service; the dispatcher resolves all of them.
// Every time value comes from the caller's TimeProvider; implementations never use the database clock.
public interface IOutboxStore
{
    string ModuleName { get; }

    // Claims up to batchSize due rows for claimToken until lockedUntil and increments their Attempts.
    // Concurrent callers never receive the same row.
    Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        Guid claimToken, int batchSize, DateTimeOffset now, DateTimeOffset lockedUntil, CancellationToken cancellationToken);

    // Each returns false, and changes nothing, when claimToken no longer owns the row (lease lost).
    Task<bool> MarkProcessedAsync(Guid messageId, Guid claimToken, DateTimeOffset now, CancellationToken cancellationToken);

    Task<bool> MarkFailedAsync(
        Guid messageId, Guid claimToken, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken);

    Task<bool> MarkDeadLetteredAsync(
        Guid messageId, Guid claimToken, string error, DateTimeOffset now, CancellationToken cancellationToken);
}
