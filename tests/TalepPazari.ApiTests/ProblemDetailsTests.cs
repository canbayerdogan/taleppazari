using System.Net.Http.Json;
using System.Text;
using TalepPazari.ApiTests.Support;

namespace TalepPazari.ApiTests;

// Every non-success response is application/problem+json in one shape. No database needed.
public sealed class ProblemDetailsTests
{
    private const string ItemsRoute = "/api/v1/example/items";

    [Fact]
    public async Task Validation_failure_returns_400_problem_with_errors()
    {
        await using var api = TestApi.Create();

        var response = await api.Client.PostAsJsonAsync(ItemsRoute, new { name = "" }, TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.HasShapeAsync(response, 400, withErrors: true);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task Malformed_json_returns_400_problem()
    {
        await using var api = TestApi.Create();

        var response = await api.Client.PostAsync(ItemsRoute, Json("{\"name\":"), TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 400);
    }

    [Fact]
    public async Task Missing_body_returns_400_problem()
    {
        await using var api = TestApi.Create();

        var response = await api.Client.PostAsync(ItemsRoute, Json(""), TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 400);
    }

    [Fact]
    public async Task Wrong_content_type_returns_415_problem()
    {
        await using var api = TestApi.Create();

        var content = new StringContent("{\"name\":\"x\"}", Encoding.UTF8, "text/plain");
        var response = await api.Client.PostAsync(ItemsRoute, content, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 415);
    }

    [Theory]
    [InlineData("/api/v1/does-not-exist")]
    [InlineData("/api/v2/example/items")]
    [InlineData("/example/items")]
    public async Task Unknown_route_returns_404_problem(string path)
    {
        await using var api = TestApi.Create();

        var response = await api.Client.GetAsync(path, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 404);
    }

    [Fact]
    public async Task Method_not_allowed_returns_405_problem()
    {
        await using var api = TestApi.Create();

        var response = await api.Client.DeleteAsync(ItemsRoute, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 405);
    }

    [Fact]
    public async Task Unparsable_query_value_returns_400_problem()
    {
        await using var api = TestApi.Create();

        var response = await api.Client.GetAsync($"{ItemsRoute}?limit=abc", TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 400);
    }

    [Fact]
    public async Task OpenApi_document_is_not_served_outside_Development()
    {
        await using var api = TestApi.Create();

        var response = await api.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 404);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
