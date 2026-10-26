using TalepPazari.Modules.Example.Infrastructure;
using TalepPazari.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Modules are registered explicitly, one extension method per module.
builder.Services.AddExampleModule(builder.Configuration);

builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
host.Run();
