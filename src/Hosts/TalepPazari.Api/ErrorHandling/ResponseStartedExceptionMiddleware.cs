using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using TalepPazari.Api.Observability;

namespace TalepPazari.Api.ErrorHandling;

// An exception after the response started cannot become problem details: the status line is already sent.
// The framework's exception handler rethrows such exceptions and the server then logs the full exception
// (message included) at Error. This middleware runs inside the exception handler, logs per
// UnhandledExceptionLog (type and route template; full exception at Debug), aborts the request so the
// client sees a broken response instead of a truncated one that looks complete, and ends the exception.
// Because the exception ends here, hosting diagnostics would see a finished request; so the failure is
// made visible to telemetry: the request activity gets status Error and an error.type tag (the exception
// type; no status description, no exception event, never the message), the framework's request-duration
// metric gets the same error.type tag, and the TalepPazari.Api counter api.responses.failed_after_start
// counts it per route template.
// Exceptions before the response started, and client aborts, pass through unchanged.
internal sealed class ResponseStartedExceptionMiddleware(
    RequestDelegate next, ApiMetrics metrics, ILogger<ResponseStartedExceptionMiddleware> logger)
{
    public const string ErrorTypeTag = "error.type";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            var routeTemplate = UnhandledExceptionLog.RouteTemplateOf(context.GetEndpoint());
            var errorType = exception.GetType().FullName;
            UnhandledExceptionLog.Unhandled(logger, exception, routeTemplate);

            Activity.Current?.SetStatus(ActivityStatusCode.Error).SetTag(ErrorTypeTag, errorType);
            context.Features.Get<IHttpMetricsTagsFeature>()?.Tags.Add(new(ErrorTypeTag, errorType));
            metrics.FailedAfterResponseStarted.Add(1, new KeyValuePair<string, object?>(ApiMetrics.RouteTag, routeTemplate));

            context.Items[RequestLogMiddleware.ServerAbortedKey] = true;
            context.Abort();
        }
    }
}
