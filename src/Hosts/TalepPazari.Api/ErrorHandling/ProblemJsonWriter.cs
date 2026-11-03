using System.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
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

    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var httpContext = context.HttpContext;
        var problem = context.ProblemDetails;

        // Same defaults as the framework: status from the response, RFC 9110 type and title, traceId.
        problem.Status ??= httpContext.Response.StatusCode;
        var defaults = TypedResults.Problem(statusCode: problem.Status).ProblemDetails;
        problem.Type ??= defaults.Type;
        problem.Title ??= defaults.Title;
        problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? httpContext.TraceIdentifier);

        // Runtime type, so HttpValidationProblemDetails keeps its "errors" member.
        return new ValueTask(httpContext.Response.WriteAsJsonAsync(
            problem, problem.GetType(), jsonOptions.Value.SerializerOptions, ContentType, httpContext.RequestAborted));
    }
}
