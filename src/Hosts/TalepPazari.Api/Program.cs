using TalepPazari.Modules.Example.Api;
using TalepPazari.Modules.Example.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

// Modules are registered explicitly, one extension method per module.
builder.Services.AddExampleModule(builder.Configuration);

var app = builder.Build();

// Liveness placeholder; health check conventions are defined in T-008.
app.MapHealthChecks("/health/live");

app.MapExampleEndpoints();

app.Run();
