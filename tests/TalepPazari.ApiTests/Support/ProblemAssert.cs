using System.Text.Json;

namespace TalepPazari.ApiTests.Support;

// The one problem-details shape every non-success response has (docs/architecture/api-conventions.md).
public static class ProblemAssert
{
    private static readonly string[] BaseMembers = ["status", "title", "traceId", "type"];

    public static async Task<JsonElement> HasShapeAsync(HttpResponseMessage response, int expectedStatus, bool withErrors = false)
    {
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement.Clone();
        var root = JsonDocument.Parse(body).RootElement.Clone();

        string[] expected = withErrors ? [.. BaseMembers, "errors"] : BaseMembers;
        var actual = root.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal);
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual);

        Assert.Equal(expectedStatus, root.GetProperty("status").GetInt32());
        Assert.StartsWith("https://", root.GetProperty("type").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        return root;
    }
}
