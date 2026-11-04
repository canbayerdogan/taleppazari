using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TalepPazari.Worker.Outbox;

// BCL hooks; the Worker's OpenTelemetry setup (WorkerComposition) subscribes to this source and meter.
internal static class OutboxTelemetry
{
    public const string Name = "TalepPazari.Outbox";

    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    public static readonly Counter<long> Dispatched = Meter.CreateCounter<long>("outbox.messages.dispatched");

    public static readonly Counter<long> Failed = Meter.CreateCounter<long>("outbox.messages.failed");

    public static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>("outbox.messages.dead_lettered");
}
