namespace TalepPazari.Worker.Outbox;

// Outcome of one dispatcher pass. FailedStores counts modules whose outbox could not be read or updated.
public sealed record OutboxDispatchResult(int Claimed, int FailedStores);
