namespace TalepPazari.Api.ErrorHandling;

// An exception after the response started cannot become problem details: the status line is already sent.
// The framework's exception handler rethrows such exceptions and the server then logs the full exception
// (message included) at Error. This middleware runs inside the exception handler, logs per
// UnhandledExceptionLog (type and route template; full exception at Debug), aborts the request so the
// client sees a broken response instead of a truncated one that looks complete, and ends the exception.
// Exceptions before the response started, and client aborts, pass through unchanged.
internal sealed class ResponseStartedExceptionMiddleware(RequestDelegate next, ILogger<ResponseStartedExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            UnhandledExceptionLog.Unhandled(logger, exception, UnhandledExceptionLog.RouteTemplateOf(context.GetEndpoint()));
            context.Abort();
        }
    }
}
