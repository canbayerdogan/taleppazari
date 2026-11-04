using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalepPazari.ApiTests.Support;
using TalepPazari.IntegrationTests.Support;

namespace TalepPazari.ApiTests;

// How aborted requests show in logs and telemetry. The failing endpoints exist only in this test host
// (terminal branches after the host's pipeline, so inside its error handling). No database needed.
//   - Cancelled by the client: the framework's exception handler ends it with 499 and never calls the
//     host's handler; the host logs it at Debug, counts api.requests.client_aborted, and it is no error.
//   - Exception after the response started: the host aborts the request; the activity has status Error and
//     an error.type tag, api.responses.failed_after_start counts it, and the message is nowhere.
public sealed class RequestAbortTelemetryTests
{
    private const string Secret = "secret-a91c-in-the-exception-message";
    private const string WaitsForClientPath = "/test-only/waits-for-client";
    private const string ServerCancelsPath = "/test-only/server-cancels";
    private const string StartedThenThrowsPath = "/test-only/started-then-throws";
    private const string ClientAborted = "api.requests.client_aborted";
    private const string FailedAfterStart = "api.responses.failed_after_start";

    [Fact]
    public async Task A_request_cancelled_by_the_client_is_logged_at_Debug_counted_and_not_an_error()
    {
        var endpoints = new TestEndpoints();
        await using var api = TestApi.Create(configureServices: services => services.AddSingleton<IStartupFilter>(endpoints));
        using var telemetry = new TelemetryCapture(api);

        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, WaitsForClientPath);
        request.Headers.Add("traceparent", telemetry.TraceParent);
        var send = api.Client.SendAsync(request, cancel.Token);
        await endpoints.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);

        var activity = await telemetry.ActivityStopped.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var requestLog = await WaitForRequestLogAsync(api);

        Assert.Equal(LogLevel.Debug, requestLog.Level);
        Assert.Contains("cancelled by the client", requestLog.Message, StringComparison.Ordinal);
        var entries = api.Logs.Entries.ToList();
        Assert.DoesNotContain(entries, entry => entry.Level >= LogLevel.Warning);
        Assert.DoesNotContain(entries, entry => entry.Exception is not null && entry.Level > LogLevel.Debug);

        Assert.Equal(1, telemetry.Sum(ClientAborted));
        Assert.Equal(0, telemetry.Sum(FailedAfterStart));
        Assert.Contains(telemetry.Measurements, m => m.Instrument == ClientAborted && m.Tags.Contains(new("http.route", "(no route)")));

        Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.GetTagItem("error.type"));
    }

    // Cancellation the client did not cause (for example an internal timeout) is a server failure:
    // a 500 problem and one Error entry with the type only (T-008a behavior), not counted as a client abort.
    [Fact]
    public async Task A_cancellation_the_client_did_not_cause_stays_a_500_with_one_Error_entry()
    {
        var endpoints = new TestEndpoints();
        await using var api = TestApi.Create(configureServices: services => services.AddSingleton<IStartupFilter>(endpoints));
        using var telemetry = new TelemetryCapture(api);

        using var response = await api.Client.GetAsync(ServerCancelsPath, TestContext.Current.CancellationToken);

        Assert.Equal(500, (int)response.StatusCode);
        var error = Assert.Single(api.Logs.Entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Contains(typeof(OperationCanceledException).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Equal(0, telemetry.Sum(ClientAborted));
    }

    [Fact]
    public async Task An_exception_after_the_response_started_is_an_error_in_the_activity_and_the_counter_without_the_message()
    {
        var endpoints = new TestEndpoints();
        await using var api = TestApi.Create(configureServices: services => services.AddSingleton<IStartupFilter>(endpoints));
        using var telemetry = new TelemetryCapture(api);

        using var request = new HttpRequestMessage(HttpMethod.Get, StartedThenThrowsPath);
        request.Headers.Add("traceparent", telemetry.TraceParent);
        await Record.ExceptionAsync(async () =>
        {
            using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        });

        var activity = await telemetry.ActivityStopped.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await WaitForRequestLogAsync(api);

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
        Assert.Equal(typeof(LeakyTestException).FullName, activity.GetTagItem("error.type"));
        Assert.Empty(activity.Events);
        Assert.All(activity.TagObjects, tag => Assert.DoesNotContain(Secret, tag.Value?.ToString() ?? string.Empty, StringComparison.Ordinal));

        Assert.Equal(1, telemetry.Sum(FailedAfterStart));
        Assert.Equal(0, telemetry.Sum(ClientAborted));
        Assert.All(telemetry.Measurements, m => Assert.Equal(["http.route"], m.Tags.Select(tag => tag.Key)));
        Assert.All(api.Logs.Entries.Where(entry => entry.Level > LogLevel.Debug), entry =>
            Assert.DoesNotContain(Secret, entry.AllText, StringComparison.Ordinal));
    }

    private static async Task<LogEntry> WaitForRequestLogAsync(TestApi api)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            var entry = api.Logs.Entries.FirstOrDefault(e => e.Category == "TalepPazari.Api.Observability.RequestLogMiddleware");
            if (entry is not null)
            {
                return entry;
            }

            Assert.True(DateTime.UtcNow < deadline, "the request log entry was not written");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    private sealed class LeakyTestException(string message) : Exception(message);

    private sealed class TestEndpoints : IStartupFilter
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(WaitsForClientPath, branch => branch.Run(async context =>
            {
                Reached.TrySetResult();
                await Task.Delay(Timeout.Infinite, context.RequestAborted);
            }));
            app.Map(ServerCancelsPath, branch => branch.Run(_ => throw new OperationCanceledException(Secret)));
            app.Map(StartedThenThrowsPath, branch => branch.Run(async context =>
            {
                await context.Response.WriteAsync("partial", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
                throw new LeakyTestException(Secret);
            }));
        };
    }
}
