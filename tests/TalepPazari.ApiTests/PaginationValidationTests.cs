using TalepPazari.ApiTests.Support;

namespace TalepPazari.ApiTests;

// Invalid paging input is rejected before any query runs, so no database is needed.
public sealed class PaginationValidationTests
{
    private const string ItemsRoute = "/api/v1/example/items";

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Out_of_range_limit_returns_400_problem(int limit)
    {
        await using var api = TestApi.Create();

        var response = await api.Client.GetAsync($"{ItemsRoute}?limit={limit}", TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.HasShapeAsync(response, 400, withErrors: true);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Limit", out _));
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("AQ")] // version byte only
    [InlineData("AgAAAAAAAAAAAAAAAAAAAAE")] // unknown format version 2
    [InlineData("AQAAAAAAAAAAAAAAAAAAAAA")] // version 1, empty id
    [InlineData("AQAAAAAAAAAAAAAAAAAAAAAB")] // one byte too long
    [InlineData("%20")]
    public async Task Invalid_cursor_returns_400_problem(string cursor)
    {
        await using var api = TestApi.Create();

        var response = await api.Client.GetAsync($"{ItemsRoute}?cursor={cursor}", TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.HasShapeAsync(response, 400, withErrors: true);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Cursor", out _));
    }
}
