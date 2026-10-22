var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

// Liveness placeholder; health check conventions are defined in T-008.
app.MapHealthChecks("/health/live");

app.Run();
