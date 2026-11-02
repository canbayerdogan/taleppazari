using TalepPazari.Modules.Example.Infrastructure;
using TalepPazari.Worker;
using TalepPazari.Worker.Outbox;

var builder = Host.CreateApplicationBuilder(args);

// Modules are registered explicitly, one extension method per module.
// Migrations are never applied here (see docs/module-template/how-to-add-a-module.md).
builder.Services.AddExampleModule(builder.Configuration);

builder.Services.AddOutboxDispatcher(builder.Configuration);
builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
host.Run();
