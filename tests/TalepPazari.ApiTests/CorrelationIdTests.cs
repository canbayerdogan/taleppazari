using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalepPazari.ApiTests.Support;
using TalepPazari.IntegrationTests.Support;
using TalepPazari.Modules.Example.Application.ExampleItems;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.ApiTests;

// X-Correlation-ID (ADR-006 name, ADR-007 rule): 1-64 characters from [A-Za-z0-9._-], exactly one value.
// No database needed (the outbox part is in DatabaseObservabilityTests).
public sealed class CorrelationIdTests
{
    private const string Header = "X-Correlation-ID";
    private const string ItemsRoute = "/api/v1/example/items";

    // Every rejected value carries this marker, so the tests can prove it reaches no log entry.
    private const string RejectedMarker = "rej7f3a";

    public static TheoryData<string> ValidIds => new()
    {
        "a",
        new string('x', 64),
        "abc-DEF_1.2",
        "0190f5c3-1d2e-7a4b-9c8d-0123456789ab",
        "4bf92f3577b34da6a3ce929d0e0e4736",
    };

    public static TheoryData<string> InvalidIds => new()
    {
        RejectedMarker + new string('x', 64 - RejectedMarker.Length + 1), // 65 characters
        RejectedMarker + " space",
        RejectedMarker + ";semicolon",
        RejectedMarker + ",comma",
        RejectedMarker + "/slash",
        "<" + RejectedMarker + ">",
        RejectedMarker + "%0d%0aInjected: yes",
    };

    // status: 200 (liveness), 400 (validation), 404 (no route), 500 (repository throws).
    public static TheoryData<int> Statuses => new() { 200, 400, 404, 500 };

    [Theory]
    [MemberData(nameof(ValidIds))]
    public async Task A_valid_incoming_id_is_kept_and_returned(string correlationId)
    {
        await using var api = TestApi.Create();

        using var response = await SendAsync(api, 400, request => request.Headers.Add(Header, correlationId));

        Assert.Equal(correlationId, ResponseId(response));
        Assert.Contains(api.Logs.Entries, entry => Equals(entry.Scope("CorrelationId"), correlationId));
    }

    [Theory]
    [MemberData(nameof(InvalidIds))]
    public async Task An_invalid_incoming_id_is_replaced_and_never_logged(string correlationId)
    {
        await using var api = TestApi.Create();

        using var response = await SendAsync(api, 400, request => request.Headers.TryAddWithoutValidation(Header, correlationId));

        AssertGenerated(ResponseId(response));
        Assert.All(api.Logs.Entries, entry => Assert.DoesNotContain(RejectedMarker, entry.AllText, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Two_header_values_are_replaced_even_when_each_is_valid()
    {
        await using var api = TestApi.Create();

        using var response = await SendAsync(api, 400, request => request.Headers.TryAddWithoutValidation(Header, ["first", "second"]));

        var id = ResponseId(response);
        AssertGenerated(id);
        Assert.NotEqual("first", id);
    }

    [Fact]
    public async Task An_empty_or_missing_id_is_generated()
    {
        await using var api = TestApi.Create();

        using var missing = await SendAsync(api, 400, _ => { });
        using var empty = await SendAsync(api, 400, request => request.Headers.TryAddWithoutValidation(Header, string.Empty));

        AssertGenerated(ResponseId(missing));
        AssertGenerated(ResponseId(empty));
        Assert.NotEqual(ResponseId(missing), ResponseId(empty));
    }

    // The exception handler clears the response headers before it writes a 500; the header is added in
    // Response.OnStarting, so it is on every status.
    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task The_header_is_on_every_status(int status)
    {
        const string correlationId = "corr-status-check";
        await using var api = TestApi.Create(configureServices: ThrowFromRepository);

        using var sent = await SendAsync(api, status, request => request.Headers.Add(Header, correlationId));
        using var generated = await SendAsync(api, status, _ => { });

        Assert.Equal(status, (int)sent.StatusCode);
        Assert.Equal(correlationId, ResponseId(sent));
        Assert.Equal(status, (int)generated.StatusCode);
        AssertGenerated(ResponseId(generated));
    }

    // The request's entries (exception handler and request log) carry the correlation ID and the trace id
    // of the problem's traceId, so a problem response can be found in the logs.
    [Fact]
    public async Task Entries_of_a_failing_request_carry_the_correlation_id_and_the_problem_trace_id()
    {
        const string correlationId = "corr-500-entries";
        await using var api = TestApi.Create(configureServices: ThrowFromRepository);

        using var response = await SendAsync(api, 500, request => request.Headers.Add(Header, correlationId));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var traceId = body.GetProperty("traceId").GetString()!.Split('-')[1];
        var error = Assert.Single(api.Logs.Entries, entry => entry.Level == LogLevel.Error);
        var requestLog = Assert.Single(api.Logs.Entries, entry => entry.Category == "TalepPazari.Api.Observability.RequestLogMiddleware");
        Assert.All([error, requestLog], entry =>
        {
            Assert.Equal(correlationId, entry.Scope("CorrelationId"));
            Assert.Equal(traceId, entry.Scope("TraceId"));
        });
        Assert.Contains("responded 500", requestLog.Message, StringComparison.Ordinal);
    }

    private static string ResponseId(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues(Header));

    private static void AssertGenerated(string id) => Assert.Matches("^[0-9a-f]{32}$", id);

    private static async Task<HttpResponseMessage> SendAsync(TestApi api, int status, Action<HttpRequestMessage> configure)
    {
        using var request = status switch
        {
            200 => new HttpRequestMessage(HttpMethod.Get, "/health/live"),
            400 => new HttpRequestMessage(HttpMethod.Get, $"{ItemsRoute}?limit=0"),
            404 => new HttpRequestMessage(HttpMethod.Get, "/no-such-route"),
            500 => new HttpRequestMessage(HttpMethod.Post, ItemsRoute) { Content = JsonContent.Create(new { name = "item" }) },
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        configure(request);
        return await api.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static void ThrowFromRepository(IServiceCollection services) =>
        services.AddScoped<IExampleItemRepository, ThrowingRepository>();

    private sealed class ThrowingRepository : IExampleItemRepository
    {
        public void Add(ExampleItem item) => throw new InvalidOperationException("Simulated repository failure.");
    }
}
