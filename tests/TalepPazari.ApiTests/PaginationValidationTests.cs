using TalepPazari.ApiTests.Support;

namespace TalepPazari.ApiTests;

// Invalid paging input is rejected before any query runs, so no database is needed.
public sealed class PaginationValidationTests
{
    private const string ItemsRoute = "/api/v1/example/items";

    // A well-formed cursor is 23 base64url characters: version byte 1 plus a 16-byte id.
    public static TheoryData<string, string> InvalidCursors => new()
    {
        { "not-a-cursor", "valid alphabet, wrong length" },
        { "@@@@", "characters outside the base64url alphabet" },
        { "A", "single character" },
        { "AQ!A", "one invalid character" },
        { "AQ", "version byte only" },
        { "AQAAAAAAAAAAAAAAAAAAAAA", "version 1, empty id" },
        { "AgAAAAAAAAAAAAAAAAAAAAE", "unknown format version 2" },
        { "AQAAAAAAAAAAAAAAAAAAAAAB", "one byte too long" },
        { "AQGheBgp8XhXrc9Z5pZSBtg=", "padding (non-canonical)" },
        { "AQGheBgp8XhXrc9Z5pZSBt h", "whitespace inside (non-canonical)" },
        { " AQGheBgp8XhXrc9Z5pZSBtg", "leading whitespace (non-canonical)" },
        { "AQGheBgp8XhXrc9Z5pZSBth", "non-zero unused trailing bits (non-canonical)" },
        { "AQGheBgp8XhXrc9Z5pZSBtg+", "standard base64 character" },
        { new string('A', 10_000), "very long string" },
    };

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Out_of_range_limit_returns_400_problem(int limit)
    {
        await using var api = TestApi.Create();

        using var response = await api.Client.GetAsync($"{ItemsRoute}?limit={limit}", TestContext.Current.CancellationToken);
        var response = await api.Client.GetAsync($"{ItemsRoute}?limit={limit}", TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.HasShapeAsync(response, 400, withErrors: true);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Limit", out _));
    }

    [Theory]
    [MemberData(nameof(InvalidCursors))]
    public async Task Invalid_cursor_returns_400_problem(string cursor, string reason)
    {
        await using var api = TestApi.Create();

        using var response = await api.Client.GetAsync(
            $"{ItemsRoute}?cursor={Uri.EscapeDataString(cursor)}", TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.HasShapeAsync(response, 400, withErrors: true);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Cursor", out _), reason);
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
