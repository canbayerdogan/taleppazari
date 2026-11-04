using System.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace TalepPazari.Api.ErrorHandling;

// Writes every problem as application/problem+json, whatever the request's Accept header says.
// The framework's default writer declines when Accept does not allow JSON (for example "text/html"); then
// status code pages fall back to text/plain and the exception handler fails to write. Registered before
// AddProblemDetails, so IProblemDetailsService always picks this writer (status code pages, exception
// handler and TypedResults.ValidationProblem all go through that service).
internal sealed class ProblemJsonWriter(IOptions<JsonOptions> jsonOptions) : IProblemDetailsWriter
{
    private const string ContentType = "application/problem+json";
    private const string AboutBlank = "about:blank";

    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var httpContext = context.HttpContext;
        var problem = context.ProblemDetails;

        // Same defaults as the framework: status from the response, RFC 9110 type and title, traceId.
        // For a status outside the framework's table (for example 429) the framework sets no type: use
        // "about:blank" (RFC 9457 section 4.2.1). Its title is then the HTTP reason phrase; a status without
        // one (for example 599) gets a fixed title, so title is never empty.
        problem.Status ??= httpContext.Response.StatusCode;
        var defaults = TypedResults.Problem(statusCode: problem.Status).ProblemDetails;
        problem.Type ??= defaults.Type ?? AboutBlank;
        problem.Title ??= string.IsNullOrEmpty(defaults.Title) ? FallbackTitle(problem.Status.Value) : defaults.Title;
        problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? httpContext.TraceIdentifier);

        // Runtime type, so HttpValidationProblemDetails keeps its "errors" member.
        return new ValueTask(httpContext.Response.WriteAsJsonAsync(
            problem, problem.GetType(), jsonOptions.Value.SerializerOptions, ContentType, httpContext.RequestAborted));
    }

    private static string FallbackTitle(int status) =>
        ReasonPhrases.GetReasonPhrase(status) is { Length: > 0 } reasonPhrase ? reasonPhrase : "Error";
}
