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

        // Exactly 23 characters, so the length check passes and the decoder's own checks decide.
        { "AQGheBgp8XhX c9Z5pZSBtg", "23 characters, space replaces a middle character" },
        { "AQGheBgp8XhXrc9Z5pZSBt ", "23 characters, space at the last position" },
        { "AQGheBgp8XhX=c9Z5pZSBtg", "23 characters, '=' replaces a middle character" },
        { "AQGheBgp8XhXrc9Z5pZSBt=", "23 characters, '=' at the last position" },
        { "AQGheBgp8XhXrc9Z5pZSBt!", "23 characters, invalid character at the last position" },
        { "AQGheBgp8XhXrc9Z5pZSBt+", "23 characters, standard base64 character at the last position" },
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
    }

    // An empty cursor binds as an empty string (not as "no cursor"), so it is an invalid cursor, not page one.
    [Theory]
    [InlineData("?cursor=")]
    [InlineData("?cursor")]
    [InlineData("?limit=5&cursor=")]
    public async Task Empty_cursor_returns_400_problem(string query)
    {
        await using var api = TestApi.Create();

        using var response = await api.Client.GetAsync($"{ItemsRoute}{query}", TestContext.Current.CancellationToken);

        var problem = await ProblemAssert.HasShapeAsync(response, 400, withErrors: true);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Cursor", out _));
    }
}
