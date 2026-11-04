using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using TalepPazari.Api.ErrorHandling;

namespace TalepPazari.Api.Observability;

// The host's own request log. It replaces the framework's request log, which LogPolicy keeps below its
// enabled level because it prints the URL with its query string. One entry per request: method, route
// template (never the path or the query string), status and duration; inside the correlation scope.
// A request the client cancelled is logged at Debug, counted, and not treated as an error. A request the
// server aborted (ResponseStartedExceptionMiddleware) is marked through HttpContext.Items and not counted here.
internal sealed partial class RequestLogMiddleware(RequestDelegate next, ApiMetrics metrics, ILogger<RequestLogMiddleware> logger)
{
    public static readonly object ServerAbortedKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var method = MethodOf(context.Request.Method);
            var routeTemplate = UnhandledExceptionLog.RouteTemplateOf(
                context.GetEndpoint() ?? context.Features.Get<IExceptionHandlerFeature>()?.Endpoint);

            if (context.RequestAborted.IsCancellationRequested && !context.Items.ContainsKey(ServerAbortedKey))
            {
                metrics.ClientAborted.Add(1, new KeyValuePair<string, object?>(ApiMetrics.RouteTag, routeTemplate));
                LogClientAborted(logger, method, routeTemplate, elapsed);
            }
            else
            {
                LogFinished(logger, method, routeTemplate, context.Response.StatusCode, elapsed);
            }
        }
    }

    // The method comes from the client: unknown values are not logged verbatim.
    private static string MethodOf(string method) => method switch
    {
        _ when HttpMethods.IsGet(method) => HttpMethods.Get,
        _ when HttpMethods.IsPost(method) => HttpMethods.Post,
        _ when HttpMethods.IsPut(method) => HttpMethods.Put,
        _ when HttpMethods.IsPatch(method) => HttpMethods.Patch,
        _ when HttpMethods.IsDelete(method) => HttpMethods.Delete,
        _ when HttpMethods.IsHead(method) => HttpMethods.Head,
        _ when HttpMethods.IsOptions(method) => HttpMethods.Options,
        _ => "_OTHER",
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "HTTP {Method} {RouteTemplate} responded {StatusCode} in {ElapsedMilliseconds:0.0} ms")]
    private static partial void LogFinished(ILogger logger, string method, string routeTemplate, int statusCode, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Debug, Message = "HTTP {Method} {RouteTemplate} cancelled by the client after {ElapsedMilliseconds:0.0} ms")]
    private static partial void LogClientAborted(ILogger logger, string method, string routeTemplate, double elapsedMilliseconds);
}
