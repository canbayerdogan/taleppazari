namespace TalepPazari.IntegrationTests.Support;

// A clock the test moves by hand, so lease and retry timing is deterministic.
// Starts on a whole second because PostgreSQL stores timestamps with microsecond precision.
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan duration) => now += duration;
}
