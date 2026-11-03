namespace TalepPazari.Api.ErrorHandling;

// T-007b rule: Warning and Error entries carry the exception type and the route template only (never the
// exception message, the query string or the body); the full exception is logged at Debug only.
internal static partial class UnhandledExceptionLog
{
    public static void Unhandled(ILogger logger, Exception exception, string routeTemplate)
    {
        LogUnhandled(logger, exception.GetType().FullName, routeTemplate);
        Details(logger, exception, routeTemplate);
    }

    public static string RouteTemplateOf(Endpoint? endpoint) =>
        (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "(no route)";

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled {ExceptionType} on route {RouteTemplate}.")]
    private static partial void LogUnhandled(ILogger logger, string? exceptionType, string routeTemplate);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Exception details for route {RouteTemplate}.")]
    public static partial void Details(ILogger logger, Exception exception, string routeTemplate);
}
