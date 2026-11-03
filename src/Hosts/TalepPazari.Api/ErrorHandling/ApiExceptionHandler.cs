using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TalepPazari.Api.ErrorHandling;

// Turns every exception that escapes an endpoint before the response started into a problem-details
// response. The response never contains the exception message, its type or a stack trace. Logging follows
// UnhandledExceptionLog. Exceptions after the response started never reach this handler (the framework
// rethrows them first); ResponseStartedExceptionMiddleware handles those.
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var routeTemplate = UnhandledExceptionLog.RouteTemplateOf(httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint);

        int status;
        if (exception is BadHttpRequestException badRequest)
        {
            // A client error (unreadable body, wrong content type, body too large): not a server fault.
            status = badRequest.StatusCode;
            UnhandledExceptionLog.Details(logger, exception, routeTemplate);
        }
        else
        {
            status = StatusCodes.Status500InternalServerError;
            UnhandledExceptionLog.Unhandled(logger, exception, routeTemplate);
        }

        // ProblemJsonWriter accepts every request, so this writes whatever the Accept header says.
        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status },
        });
    }
}
