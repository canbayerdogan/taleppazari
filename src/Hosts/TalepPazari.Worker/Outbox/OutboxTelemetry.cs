using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TalepPazari.Worker.Outbox;

// BCL hooks only; exporters and final names belong to T-008.
internal static class OutboxTelemetry
{
    public const string Name = "TalepPazari.Outbox";

    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    public static readonly Counter<long> Dispatched = Meter.CreateCounter<long>("outbox.messages.dispatched");

    public static readonly Counter<long> Failed = Meter.CreateCounter<long>("outbox.messages.failed");

    public static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>("outbox.messages.dead_lettered");
}
