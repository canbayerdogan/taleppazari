using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TalepPazari.Api.Observability;

// OpenTelemetry traces and metrics (ADR-007). No vendor SDK; the backend is not chosen (ADR-007 Open).
// Telemetry:Exporter selects the exporter: "Console" (the default when unset) or "None" (telemetry is still
// collected, so in-process listeners and tests see it, but nothing is exported). Any other value stops the
// host at startup. The value is read from the final configuration when the providers are built (not while
// services are registered), so every configuration source counts, including ones added after Program.cs.
// Logs are not routed through OpenTelemetry; they stay on the JSON console logger.
// One copy per host, kept identical except for the namespace (docs/architecture/observability.md).
internal static class TelemetrySetup
{
    public const string ExporterKey = "Telemetry:Exporter";

    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        string serviceName,
        Action<TracerProviderBuilder> configureTracing,
        Action<MeterProviderBuilder> configureMetrics)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                configureTracing(tracing);
                tracing.AddProcessor(provider => IsConsole(provider)
                    ? new SimpleActivityExportProcessor(new ConsoleActivityExporter(new ConsoleExporterOptions()))
                    : new NoExportProcessor());
            })
            .WithMetrics(metrics =>
            {
                configureMetrics(metrics);
                metrics.AddReader(provider => IsConsole(provider)
                    ? new PeriodicExportingMetricReader(new ConsoleMetricExporter(new ConsoleExporterOptions()))
                    : new BaseExportingMetricReader(new NoExportMetricExporter()));
            });
        return services;
    }

    private static bool IsConsole(IServiceProvider provider)
    {
        var value = provider.GetRequiredService<IConfiguration>()[ExporterKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "CONSOLE" => true,
            "NONE" => false,
            _ => throw new InvalidOperationException($"{ExporterKey} must be \"Console\" or \"None\"."),
        };
    }

    // "None": spans are recorded for in-process listeners, nothing leaves the process.
    private sealed class NoExportProcessor : BaseProcessor<Activity>;

    // "None": metrics are only collected on demand (never, without a pull), nothing leaves the process.
    private sealed class NoExportMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch) => ExportResult.Success;
    }
}
