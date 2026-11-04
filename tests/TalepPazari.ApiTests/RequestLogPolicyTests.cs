using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TalepPazari.ApiTests.Support;

namespace TalepPazari.ApiTests;

// The request-log policy (LogPolicy, RequestLogMiddleware) holds even when configuration lowers every level
// to Trace: query strings, bodies, passwords and tokens appear in no captured entry at any level.
// No database needed (the database-backed redaction test is in DatabaseObservabilityTests).
public sealed class RequestLogPolicyTests
{
    public const string QuerySecret = "qsecret-5e1b-from-the-query";
    public const string Password = "pw-secret-91d2";
    public const string Token = "token-secret-c4a7";
    public const string BodySecret = "body-secret-08fe";

    private const string ItemsRoute = "/api/v1/example/items";

    // Configuration that would let every framework entry through if only filter rules protected the logs:
    // category rules, a more specific hosting rule and provider-specific rules.
    public static readonly IReadOnlyDictionary<string, string?> EverythingAtTrace = new Dictionary<string, string?>
    {
        ["Logging:LogLevel:Default"] = "Trace",
        ["Logging:LogLevel:Microsoft"] = "Trace",
        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Trace",
        ["Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = "Trace",
        ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Trace",
        ["Logging:Console:LogLevel:Default"] = "Trace",
        ["Logging:Console:LogLevel:Microsoft.AspNetCore"] = "Trace",
    };

    // Each puts the secret in the query string: a failed int binding (the framework logs the source value
    // at Debug), an invalid cursor (400), an unknown route (404), a POST that fails on the database (500).
    public static TheoryData<string, string> QueryRequests => new()
    {
        { "GET", $"{ItemsRoute}?limit={QuerySecret}" },
        { "GET", $"{ItemsRoute}?cursor={QuerySecret}" },
        { "GET", $"/no-such-route?token={QuerySecret}" },
        { "POST", $"{ItemsRoute}?token={QuerySecret}" },
    };

    [Theory]
    [MemberData(nameof(QueryRequests))]
    public async Task A_query_string_secret_appears_in_no_entry_at_any_level_even_with_every_level_lowered(string method, string url)
    {
        await using var api = TestApi.Create(settings: EverythingAtTrace);

        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { name = "item" });
        }

        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        var entries = api.Logs.Entries.ToList();
        Assert.All(entries, entry => Assert.DoesNotContain(QuerySecret, entry.AllText, StringComparison.Ordinal));

        // The framework categories stay at Warning and above; the host's own request log replaces them.
        Assert.DoesNotContain(entries, entry =>
            entry.Category.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) && entry.Level < LogLevel.Warning);
        var requestLog = Assert.Single(entries, entry => entry.Category == "TalepPazari.Api.Observability.RequestLogMiddleware");
        Assert.Contains($"responded {(int)response.StatusCode}", requestLog.Message, StringComparison.Ordinal);
    }

    // Without a database (the POST fails with a 500, whose details are logged at Debug).
    [Fact]
    public async Task A_password_a_token_and_the_body_appear_in_no_entry_at_any_level()
    {
        await using var api = TestApi.Create(settings: EverythingAtTrace);

        using var request = SecretBearingCreateRequest();
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(500, (int)response.StatusCode);
        LogAssert.NoTextInAnyEntry(api.Logs.Entries, Password, Token, BodySecret);
    }

    // A create request that carries a password and a body text in its JSON body and a token in its
    // Authorization and Cookie headers. The item name is the body secret too.
    public static HttpRequestMessage SecretBearingCreateRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ItemsRoute)
        {
            Content = JsonContent.Create(new { name = BodySecret, password = Password, message = BodySecret }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        request.Headers.TryAddWithoutValidation("Cookie", $"session={Token}; password={Password}");
        return request;
    }
}
