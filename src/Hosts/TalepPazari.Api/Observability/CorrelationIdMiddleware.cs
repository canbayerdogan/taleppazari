using System.Buffers;
using Microsoft.Extensions.Primitives;

namespace TalepPazari.Api.Observability;

// ADR-007 correlation rule; header name from ADR-006. Runs first in the pipeline.
// An incoming value is kept only if the request has exactly one X-Correlation-ID value of 1-64 characters
// from [A-Za-z0-9._-]; otherwise a new UUID v7 ("N" format, 32 hex digits) is used. A rejected value is never
// logged (log injection, oversized values). The accepted value is set on the request's CorrelationContext,
// added to the log scope of every entry written while the request runs, and returned in the response header.
// The header is added in Response.OnStarting, because the exception handler clears the headers before it
// writes a 500.
internal sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const int MaxLength = 64;

    private static readonly SearchValues<char> AllowedCharacters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789._-");

    public async Task InvokeAsync(HttpContext context, CorrelationContext correlationContext)
    {
        var incoming = context.Request.Headers[HeaderName];
        string correlationId;
        if (IsValid(incoming))
        {
            correlationId = incoming[0]!;
        }
        else
        {
            correlationId = Guid.CreateVersion7().ToString("N");
            if (incoming.Count > 0)
            {
                LogRejected(logger);
            }
        }

        correlationContext.Set(correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    internal static bool IsValid(StringValues values) =>
        values.Count == 1
        && values[0] is { Length: > 0 and <= MaxLength } value
        && !value.AsSpan().ContainsAnyExcept(AllowedCharacters);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Incoming correlation ID rejected (count, length or characters); a new one was generated")]
    private static partial void LogRejected(ILogger logger);
}
