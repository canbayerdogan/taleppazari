using FluentValidation;
using TalepPazari.Api.ErrorHandling;
using TalepPazari.Api.Observability;
using TalepPazari.Api.OpenApi;
using TalepPazari.Modules.Example.Api;
using TalepPazari.Modules.Example.Infrastructure;
using TalepPazari.SharedKernel.Observability;

// Validation messages use FluentValidation's built-in English texts, whatever the server culture is.
// Localization is a later product decision (docs/architecture/api-conventions.md).
ValidatorOptions.Global.LanguageManager.Enabled = false;

var builder = WebApplication.CreateBuilder(args);

// Trace and span ids in every log entry's scope, so a problem's traceId can be found in the logs.
builder.Logging.Configure(options => options.ActivityTrackingOptions =
    ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId | ActivityTrackingOptions.ParentId);

builder.Services.AddHealthChecks();

// Correlation ID (ADR-007): one CorrelationContext per request, read by modules through ICorrelationContext.
builder.Services.AddScoped<CorrelationContext>();
builder.Services.AddScoped<ICorrelationContext>(provider => provider.GetRequiredService<CorrelationContext>());
builder.Services.AddSingleton<ApiMetrics>();

// Every non-success response is RFC 9457 problem details as application/problem+json, whatever the
// Accept header says (docs/architecture/api-conventions.md). The writer is registered first, so it wins.
builder.Services.AddSingleton<IProblemDetailsWriter, ProblemJsonWriter>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Minimal APIs throw on bad requests in Development by default; return the 400/415 everywhere instead,
// so status code pages turn it into problem details in every environment.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

// One OpenAPI document for this host. Served in Development only until the deployment target is chosen.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<DeterministicDocumentTransformer>());

// Modules are registered explicitly, one extension method per module.
builder.Services.AddExampleModule(builder.Configuration);

// Last: the log policy wraps the logger factory (docs/architecture/observability.md). Microsoft.AspNetCore
// is never enabled below Warning, because its request and binding logs print query strings and values.
builder.Services.AddLogPolicy("Microsoft.AspNetCore");

var app = builder.Build();

// Correlation first, so every entry of the request (also from the exception handler) carries the ID and
// every response (also a 500) gets the header; then the host's own request log.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLogMiddleware>();
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
