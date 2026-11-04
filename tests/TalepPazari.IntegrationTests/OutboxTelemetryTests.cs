using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using TalepPazari.IntegrationTests.Support;
using TalepPazari.Modules.Example.Contracts.IntegrationEvents;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.IntegrationTests;

// The outbox telemetry (TalepPazari.Outbox source and meter) seen by listeners, and the Worker's exporter switch.
// The meter and source are static, so these tests run alone (serial collection): no other test dispatches
// while they count. Closes the T-007 "no listener test" gap.
[Collection(SerialTelemetryCollection.Name)]
public sealed class OutboxTelemetryTests(PostgresFixture postgres)
{
    private const string SourceName = "TalepPazari.Outbox";

    [Fact]
    public async Task A_dispatch_emits_a_consumer_activity_in_the_request_trace_and_the_dispatched_counter()
    {
        using var capture = new OutboxCapture();
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        await host.CreateItemAsync();
        var traceId = ActivityTraceId.CreateRandom();
        var parentSpanId = ActivitySpanId.CreateRandom();
        await host.ExecuteAsync($"UPDATE example.\"OutboxMessages\" SET \"TraceParent\" = '00-{traceId}-{parentSpanId}-01'");

        await host.RunOnceAsync();

        var activity = Assert.Single(capture.Activities);
        Assert.Equal("outbox.dispatch", activity.OperationName);
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal(traceId, activity.TraceId);
        Assert.Equal(parentSpanId, activity.ParentSpanId);
        Assert.Equal(1, capture.Sum("outbox.messages.dispatched"));
        Assert.Equal(0, capture.Sum("outbox.messages.failed"));
        Assert.All(capture.Measurements, m => Assert.Contains(new KeyValuePair<string, object?>("module", "Example"), m.Tags));
    }

    [Fact]
    public async Task Failures_and_the_dead_letter_are_counted()
    {
        using var capture = new OutboxCapture();
        await using var host = await ExampleTestHost.CreateAsync(
            postgres, services => services.AddScoped<IIntegrationEventHandler<ExampleItemCreatedV1>, ThrowingHandler>());
        await host.CreateItemAsync();

        // MaxAttempts is 3: attempts 1 and 2 fail and are retried, attempt 3 is dead-lettered.
        await host.RunOnceAsync();
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        await host.RunOnceAsync();
        host.Clock.Advance(TimeSpan.FromSeconds(2));
        await host.RunOnceAsync();

        Assert.Equal(2, capture.Sum("outbox.messages.failed"));
        Assert.Equal(1, capture.Sum("outbox.messages.dead_lettered"));
        Assert.Equal(0, capture.Sum("outbox.messages.dispatched"));
        Assert.Equal(3, capture.Activities.Count);
    }

    // The real Worker composition: Console (the default when unset) exports the dispatch span; None does not.
    [Theory]
    [InlineData(null, true)]
    [InlineData("None", false)]
    public async Task The_Worker_exporter_switch_controls_the_console_export(string? exporter, bool exported)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var settings = new Dictionary<string, string?>(ExampleTestHost.DefaultOutboxSettings) { ["Telemetry:Exporter"] = exporter };
        await using var worker = WorkerTestHost.Create(connectionString, settings);
        await Modules.Example.Infrastructure.ExampleModule.MigrateDatabaseAsync(worker.Services, TestContext.Current.CancellationToken);
        await worker.CreateItemAsync();

        var original = Console.Out;
        await using var captured = new StringWriter();
        Console.SetOut(TextWriter.Synchronized(captured));
        try
        {
            // The host is not started, so the tracer provider (which subscribes to the source) is resolved here.
            _ = worker.Services.GetRequiredService<TracerProvider>();
            await worker.RunOnceAsync();
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal(exported, captured.ToString().Contains("outbox.dispatch", StringComparison.Ordinal));
    }

    private sealed class OutboxCapture : IDisposable
    {
        private readonly ActivityListener activityListener;
        private readonly MeterListener meterListener;

        public OutboxCapture()
        {
            activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Activities.Add,
            };
            ActivitySource.AddActivityListener(activityListener);

            meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == SourceName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Measurements.Add(new Measurement(instrument.Name, value, tags.ToArray())));
            meterListener.Start();
        }

        public ConcurrentBag<Activity> Activities { get; } = [];

        public ConcurrentBag<Measurement> Measurements { get; } = [];

        public long Sum(string instrument) => Measurements.Where(m => m.Instrument == instrument).Sum(m => m.Value);

        public void Dispose()
        {
            activityListener.Dispose();
            meterListener.Dispose();
        }
    }

    private sealed record Measurement(string Instrument, long Value, KeyValuePair<string, object?>[] Tags);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SerialTelemetryCollection
{
    public const string Name = "Serial telemetry";
}
