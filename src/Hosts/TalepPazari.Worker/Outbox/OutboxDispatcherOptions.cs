namespace TalepPazari.Worker.Outbox;

// Bound from the "Outbox" configuration section. No secrets belong here.
public sealed class OutboxDispatcherOptions
{
    public const string SectionName = "Outbox";

    // Wait between passes when the previous pass found nothing to do.
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    // Messages claimed per module per pass.
    public int BatchSize { get; set; } = 20;

    // The lease covers the whole claimed batch, which is handled one message after another:
    // BatchSize x worst-case handler time must stay below LeaseDuration, or another dispatcher may
    // claim the same messages again while this one is still working (harmless because of the inbox,
    // but wasted work). The dispatcher logs a warning when a batch outlives its lease.
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    // Claims per message before it is dead-lettered. Counted at claim time, so a message that
    // crashes the process also reaches the limit.
    public int MaxAttempts { get; set; } = 5;

    // Retry delay after a failed attempt n: min(RetryBaseDelay x 2^(n-1), RetryMaxDelay).
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    // Upper bound of the back-off while a module's outbox cannot be read (for example, database down).
    public TimeSpan ErrorBackoffMax { get; set; } = TimeSpan.FromMinutes(1);

    // Time allowed for writing a result (processed, failed, dead-lettered). This write does not use the
    // host's stopping token, so a shutdown does not drop the result of work that already finished.
    public TimeSpan AcknowledgeTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
