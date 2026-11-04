using TalepPazari.Modules.Example.Infrastructure;
using TalepPazari.Worker.Observability;
using TalepPazari.Worker.Outbox;

namespace TalepPazari.Worker;

// The Worker host's services, in one place, so Program.cs and the integration tests compose the same host.
public static class WorkerComposition
{
    public static HostApplicationBuilder AddWorkerServices(this HostApplicationBuilder builder)
    {
        // Trace and span ids in every log entry's scope (the dispatcher's activity continues the request's trace).
        builder.Logging.Configure(options => options.ActivityTrackingOptions =
            ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId | ActivityTrackingOptions.ParentId);

        // Modules are registered explicitly, one extension method per module.
        // Migrations are never applied here (see docs/module-template/how-to-add-a-module.md).
        builder.Services.AddExampleModule(builder.Configuration);

        builder.Services.AddOutboxDispatcher(builder.Configuration);
        builder.Services.AddHostedService<WorkerService>();

        // Last: the log policy wraps the logger factory (docs/architecture/observability.md). No HTTP surface,
        // so no category floor; the Warning+ exception rule applies to every category.
        builder.Services.AddLogPolicy();
        return builder;
    }
}
