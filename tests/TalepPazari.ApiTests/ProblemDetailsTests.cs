using System.Net.Http.Json;
using System.Text;
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
}
