using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TalepPazari.Api.ErrorHandling;

// Turns every exception that escapes an endpoint into a problem-details response.
// The response never contains the exception message, its type or a stack trace. Warning and Error logs
// carry only the exception type and the route template (never the query string or the body); the full
// exception is logged at Debug only, because an exception message may contain request data (T-007b rule).
internal sealed partial class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var routeTemplate = (httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint as RouteEndpoint)?.RoutePattern.RawText
            ?? "(no route)";

        int status;
        if (exception is BadHttpRequestException badRequest)
        {
            // A client error (unreadable body, wrong content type, body too large): not a server fault.
            status = badRequest.StatusCode;
        }
        else
        {
            status = StatusCodes.Status500InternalServerError;
            LogUnhandled(exception.GetType().FullName, routeTemplate);
        }

        LogDetails(exception, routeTemplate);

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled {ExceptionType} on route {RouteTemplate}.")]
    private partial void LogUnhandled(string? exceptionType, string routeTemplate);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Exception details for route {RouteTemplate}.")]
    private partial void LogDetails(Exception exception, string routeTemplate);
}
