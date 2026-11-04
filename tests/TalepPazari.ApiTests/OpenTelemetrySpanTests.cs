using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TalepPazari.ApiTests.Support;
using TalepPazari.Modules.Example.Application.ExampleItems;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.ApiTests;

// Request spans after the OpenTelemetry ASP.NET Core instrumentation and the host's enrichment ran: no tag,
// event or status description contains a secret from the query string or from an exception message, and
// url.query is not recorded at all. The instrumentation writes its tags on the request activity before the
// activity stops, so the BCL listener sees the final span. No database needed.
public sealed class OpenTelemetrySpanTests
{
    private const string QuerySecret = "span-qsecret-71c0";
    private const string ExceptionSecret = "span-exsecret-e93d";
    private const string ItemsRoute = "/api/v1/example/items";

    [Fact]
    public async Task A_query_string_secret_is_in_no_span_tag_and_url_query_is_not_recorded()
    {
        await using var api = TestApi.Create();
        using var telemetry = new TelemetryCapture(api);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ItemsRoute}?cursor={QuerySecret}&token={QuerySecret}");
        request.Headers.Add("traceparent", telemetry.TraceParent);
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(400, (int)response.StatusCode);
        var activity = await telemetry.ActivityStopped.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        AssertInstrumented(activity);
        AssertNoSecret(activity, QuerySecret);
    }

    [Fact]
    public async Task An_unhandled_exception_marks_the_span_as_an_error_without_its_message_or_the_query()
    {
        await using var api = TestApi.Create(configureServices: services =>
            services.AddScoped<IExampleItemRepository, ThrowingRepository>());
        using var telemetry = new TelemetryCapture(api);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ItemsRoute}?token={QuerySecret}")
        {
            Content = JsonContent.Create(new { name = "item" }),
        };
        request.Headers.Add("traceparent", telemetry.TraceParent);
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(500, (int)response.StatusCode);
        var activity = await telemetry.ActivityStopped.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        AssertInstrumented(activity);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        AssertNoSecret(activity, QuerySecret);
        AssertNoSecret(activity, ExceptionSecret);
        Assert.DoesNotContain(activity.Events, e => e.Name == "exception");
    }

    // The instrumentation ran (its tags are there), so the assertions are about its output.
    private static void AssertInstrumented(Activity activity)
    {
        Assert.NotNull(activity.GetTagItem("http.request.method"));
        Assert.NotNull(activity.GetTagItem("url.path"));
        Assert.Null(activity.GetTagItem("url.query"));
    }

    private static void AssertNoSecret(Activity activity, string secret)
    {
        Assert.All(activity.TagObjects, tag => Assert.DoesNotContain(secret, $"{tag.Key}={tag.Value}", StringComparison.Ordinal));
        Assert.DoesNotContain(secret, activity.StatusDescription ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, activity.DisplayName, StringComparison.Ordinal);
        Assert.All(activity.Events, activityEvent =>
        {
            Assert.DoesNotContain(secret, activityEvent.Name, StringComparison.Ordinal);
            Assert.All(activityEvent.Tags, tag => Assert.DoesNotContain(secret, $"{tag.Key}={tag.Value}", StringComparison.Ordinal));
        });
        Assert.All(activity.Baggage, item => Assert.DoesNotContain(secret, $"{item.Key}={item.Value}", StringComparison.Ordinal));
    }

    private sealed class ThrowingRepository : IExampleItemRepository
    {
        public void Add(ExampleItem item) => throw new InvalidOperationException($"Simulated failure {ExceptionSecret}.");
    }
}
