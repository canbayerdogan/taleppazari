using FluentValidation;
using TalepPazari.Api.ErrorHandling;
using TalepPazari.Api.OpenApi;
using TalepPazari.Modules.Example.Api;
using TalepPazari.Modules.Example.Infrastructure;

// Validation messages use FluentValidation's built-in English texts, whatever the server culture is.
// Localization is a later product decision (docs/architecture/api-conventions.md).
ValidatorOptions.Global.LanguageManager.Enabled = false;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

// Every non-success response is RFC 9457 problem details as application/problem+json, whatever the
// Accept header says (docs/architecture/api-conventions.md). The writer is registered first, so it wins.
builder.Services.AddSingleton<IProblemDetailsWriter, ProblemJsonWriter>();
// Every non-success response is RFC 9457 problem details (docs/architecture/api-conventions.md).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Minimal APIs throw on bad requests in Development by default; return the 400/415 everywhere instead,
// so status code pages turn it into problem details in every environment.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

// One OpenAPI document for this host. Served in Development only until the deployment target is chosen.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<DeterministicDocumentTransformer>());

// Modules are registered explicitly, one extension method per module.
builder.Services.AddExampleModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<ResponseStartedExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness placeholder; health check conventions are defined in T-008b.
app.MapHealthChecks("/health/live");

// The only place the API version appears. Modules add only their own prefix.
var v1 = app.MapGroup("/api/v1");
v1.MapExampleEndpoints();

app.Run();
