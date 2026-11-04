using System.Diagnostics.Metrics;

namespace TalepPazari.Api.Observability;

// Counters of the "TalepPazari.Api" meter. Created through IMeterFactory, so each host instance (and each
// test host) has its own meter. Tags are low-cardinality only: the route template, never a path or a query.
internal sealed class ApiMetrics
{
    public const string MeterName = "TalepPazari.Api";
    public const string RouteTag = "http.route";

    public ApiMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        ClientAborted = meter.CreateCounter<long>(
            "api.requests.client_aborted", description: "Requests the client cancelled before the response completed.");
        FailedAfterResponseStarted = meter.CreateCounter<long>(
            "api.responses.failed_after_start",
            description: "Requests that failed after the response started; the request was aborted.");
    }

    public Counter<long> ClientAborted { get; }

    public Counter<long> FailedAfterResponseStarted { get; }
}
