using Microsoft.EntityFrameworkCore;
using TalepPazari.Modules.Example.Infrastructure.Persistence;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.Modules.Example.Infrastructure.Messaging;

// Every time value is a parameter from the dispatcher's TimeProvider; no statement uses now().
// Each statement runs in its own implicit transaction; no transaction stays open while handlers run.
// Every Mark* statement requires the claim token and a row that is neither processed nor dead-lettered.
internal sealed class ExampleOutboxStore(ExampleDbContext dbContext) : IOutboxStore
{
    public string ModuleName => "Example";

    // FOR UPDATE SKIP LOCKED: concurrent claimers skip rows another claimer is locking, so they never
    // receive the same row. The lease (LockedUntil) keeps a claimed row away from others until it expires.
    // ORDER BY only makes old rows go first; it is not an ordering guarantee (ADR-003 Open).
    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        Guid claimToken, int batchSize, DateTimeOffset now, DateTimeOffset lockedUntil, CancellationToken cancellationToken)
    {
        var entries = await dbContext.OutboxMessages
            .FromSql($"""
                UPDATE example."OutboxMessages"
                SET "LockedUntil" = {lockedUntil}, "LockedBy" = {claimToken}, "Attempts" = "Attempts" + 1
                WHERE "Id" IN (
                    SELECT "Id" FROM example."OutboxMessages"
                    WHERE "ProcessedAt" IS NULL AND "DeadLetteredAt" IS NULL
                      AND "NextAttemptAt" <= {now}
                      AND ("LockedUntil" IS NULL OR "LockedUntil" <= {now})
                    ORDER BY "NextAttemptAt"
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entries
            .OrderBy(entry => entry.NextAttemptAt)
            .Select(entry => new OutboxMessage(
                entry.Id, entry.Type, entry.Payload, entry.OccurredAt, entry.Attempts, entry.TraceParent, entry.CorrelationId))
            .ToList();
    }

    public async Task<bool> MarkProcessedAsync(Guid messageId, Guid claimToken, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlAsync($"""
            UPDATE example."OutboxMessages"
            SET "ProcessedAt" = {now}, "LockedUntil" = NULL, "LockedBy" = NULL
            WHERE "Id" = {messageId} AND "LockedBy" = {claimToken}
              AND "ProcessedAt" IS NULL AND "DeadLetteredAt" IS NULL
            """, cancellationToken) == 1;

    public async Task<bool> MarkFailedAsync(
        Guid messageId, Guid claimToken, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlAsync($"""
            UPDATE example."OutboxMessages"
            SET "NextAttemptAt" = {nextAttemptAt}, "LastError" = {Truncate(error)}, "LockedUntil" = NULL, "LockedBy" = NULL
            WHERE "Id" = {messageId} AND "LockedBy" = {claimToken}
              AND "ProcessedAt" IS NULL AND "DeadLetteredAt" IS NULL
            """, cancellationToken) == 1;

    public async Task<bool> MarkDeadLetteredAsync(
        Guid messageId, Guid claimToken, string error, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlAsync($"""
            UPDATE example."OutboxMessages"
            SET "DeadLetteredAt" = {now}, "LastError" = {Truncate(error)}, "LockedUntil" = NULL, "LockedBy" = NULL
            WHERE "Id" = {messageId} AND "LockedBy" = {claimToken}
              AND "ProcessedAt" IS NULL AND "DeadLetteredAt" IS NULL
            """, cancellationToken) == 1;

    private static string Truncate(string error) =>
        error.Length <= OutboxEntry.LastErrorMaxLength ? error : error[..OutboxEntry.LastErrorMaxLength];
}
