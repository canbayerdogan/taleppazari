using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TalepPazari.ApiTests.Support;

namespace TalepPazari.ApiTests;

// Every non-success response is application/problem+json in one shape, whatever the Accept header says.
// No database needed.
public sealed class ProblemDetailsTests
{
    private const string ItemsRoute = "/api/v1/example/items";

    // NoAccept: the client sends no Accept header.
    public const string NoAccept = "(none)";

    public static TheoryData<string> AcceptHeaders => new() { NoAccept, "text/html", "text/plain", "application/xml" };

    [Theory]
    [MemberData(nameof(AcceptHeaders))]
    public async Task Validation_failure_returns_400_problem_with_errors(string accept)
    {
        await using var api = TestApi.Create();

        using var request = Request(HttpMethod.Post, ItemsRoute, accept, JsonContent.Create(new { name = "" }));
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.HasShapeAsync(response, 400, withErrors: true);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Name", out _));
    }

    [Theory]
    [MemberData(nameof(AcceptHeaders))]
    public async Task Malformed_json_returns_400_problem(string accept)
    {
        await using var api = TestApi.Create();

        using var request = Request(HttpMethod.Post, ItemsRoute, accept, Json("{\"name\":"));
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 400);
    }

    [Theory]
    [MemberData(nameof(AcceptHeaders))]
    public async Task Missing_body_returns_400_problem(string accept)
    {
        await using var api = TestApi.Create();

        using var request = Request(HttpMethod.Post, ItemsRoute, accept, Json(""));
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 400);
    }

    [Theory]
    [MemberData(nameof(AcceptHeaders))]
    public async Task Wrong_content_type_returns_415_problem(string accept)
    {
        await using var api = TestApi.Create();

        using var request = Request(HttpMethod.Post, ItemsRoute, accept, new StringContent("{\"name\":\"x\"}", Encoding.UTF8, "text/plain"));
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 415);
    }

    [Theory]
    [InlineData("/api/v1/does-not-exist", NoAccept)]
    [InlineData("/api/v2/example/items", NoAccept)]
    [InlineData("/example/items", NoAccept)]
    [InlineData("/api/v1/does-not-exist", "text/html")]
    [InlineData("/api/v1/does-not-exist", "text/plain")]
    public async Task Unknown_route_returns_404_problem(string path, string accept)
    {
        await using var api = TestApi.Create();

        using var request = Request(HttpMethod.Get, path, accept);
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 404);
    }

    [Theory]
    [MemberData(nameof(AcceptHeaders))]
    public async Task Method_not_allowed_returns_405_problem(string accept)
    {
        await using var api = TestApi.Create();

        using var request = Request(HttpMethod.Delete, ItemsRoute, accept);
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 405);
    }

    [Fact]
    public async Task Unparsable_query_value_returns_400_problem()
    {
        await using var api = TestApi.Create();

        using var response = await api.Client.GetAsync($"{ItemsRoute}?limit=abc", TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 400);
    }

    // Statuses outside the framework's default table (no RFC 9110 link) get type "about:blank" and the HTTP
    // reason phrase as title, or a fixed title when the status has no reason phrase (599).
    // 431 goes through ApiExceptionHandler; 429 and 599 go through status code pages.
    [Theory]
    [InlineData(OutsideTableEndpoints.HeadersTooLargePath, 431, "Request Header Fields Too Large", NoAccept)]
    [InlineData(OutsideTableEndpoints.HeadersTooLargePath, 431, "Request Header Fields Too Large", "text/html")]
    [InlineData(OutsideTableEndpoints.TooManyRequestsPath, 429, "Too Many Requests", NoAccept)]
    [InlineData(OutsideTableEndpoints.TooManyRequestsPath, 429, "Too Many Requests", "text/html")]
    [InlineData(OutsideTableEndpoints.NoReasonPhrasePath, 599, "Error", NoAccept)]
    public async Task Status_outside_the_default_table_returns_problem_with_about_blank_type(
        string path, int status, string title, string accept)
    {
        await using var api = TestApi.Create(configureServices: services =>
            services.AddSingleton<IStartupFilter, OutsideTableEndpoints>());

        using var request = Request(HttpMethod.Get, path, accept);
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.HasShapeAsync(response, status, expectedType: "about:blank");
        Assert.Equal(title, problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task OpenApi_document_is_not_served_outside_Development()
    {
        await using var api = TestApi.Create();

        using var response = await api.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 404);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string accept, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        if (accept != NoAccept)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        return request;
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    // Test host only: terminal branches after the host's own pipeline (so inside its error handling).
    // No real oversized request is sent: the 431 branch throws the kind of exception the server raises for
    // request headers over its limit.
    private sealed class OutsideTableEndpoints : IStartupFilter
    {
        public const string HeadersTooLargePath = "/test-only/headers-too-large";
        public const string TooManyRequestsPath = "/test-only/too-many-requests";
        public const string NoReasonPhrasePath = "/test-only/no-reason-phrase";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(HeadersTooLargePath, branch => branch.Run(_ =>
                throw new BadHttpRequestException("Request headers too large.", StatusCodes.Status431RequestHeaderFieldsTooLarge)));
            app.Map(TooManyRequestsPath, branch => branch.Run(context => SetStatus(context, StatusCodes.Status429TooManyRequests)));
            app.Map(NoReasonPhrasePath, branch => branch.Run(context => SetStatus(context, 599)));
        };

        private static Task SetStatus(HttpContext context, int status)
        {
            context.Response.StatusCode = status;
            return Task.CompletedTask;
        }
    }
}
