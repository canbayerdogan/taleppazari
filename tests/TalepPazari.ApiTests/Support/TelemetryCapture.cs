using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace TalepPazari.ApiTests.Support;

// BCL listeners for one test: request activities of one trace (the test sends its own traceparent header,
// so activities of tests running in parallel are ignored) and measurements of the "TalepPazari.Api" meter
// of one host (meters come from the host's IMeterFactory, so other hosts' counters are ignored).
public sealed class TelemetryCapture : IDisposable
{
    private readonly ActivityListener activityListener;
    private readonly MeterListener meterListener;

    public TelemetryCapture(TestApi api)
    {
        TraceId = ActivityTraceId.CreateRandom();
        var meterFactory = api.Services.GetRequiredService<IMeterFactory>();

        activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == TraceId)
                {
                    StoppedActivities.Enqueue(activity);
                    ActivityStopped.TrySetResult(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);

        meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "TalepPazari.Api" && ReferenceEquals(instrument.Meter.Scope, meterFactory))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            Measurements.Enqueue(new Measurement(instrument.Name, value, tags.ToArray())));
        meterListener.Start();
    }

    public ActivityTraceId TraceId { get; }

    // Send as the "traceparent" request header.
    public string TraceParent => $"00-{TraceId}-{ActivitySpanId.CreateRandom()}-01";

    public ConcurrentQueue<Activity> StoppedActivities { get; } = new();

    public TaskCompletionSource<Activity> ActivityStopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConcurrentQueue<Measurement> Measurements { get; } = new();

    public long Sum(string instrument) => Measurements.Where(m => m.Instrument == instrument).Sum(m => m.Value);

    public void Dispose()
    {
        activityListener.Dispose();
        meterListener.Dispose();
    }

    public sealed record Measurement(string Instrument, long Value, KeyValuePair<string, object?>[] Tags);
}
