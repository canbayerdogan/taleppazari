using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalepPazari.ApiTests.Support;
using TalepPazari.IntegrationTests.Support;
using TalepPazari.Modules.Example.Application.ExampleItems;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.ApiTests;

// The failing pieces exist only in this test host: a repository that throws, and an endpoint that starts
// the response and then throws. No database needed.
public sealed class UnhandledExceptionTests
{
    private const string Secret = "secret-7c1e-payload-from-the-request";
    private const string RequestData = "request-data-3b9d";
    private const string RouteTemplate = "/api/v1/example/items";
    private const string StartedThenThrowsPath = "/test-only/started-then-throws";

    private const string NoAccept = ProblemDetailsTests.NoAccept;

    // The same Accept values as the 4xx tests.
    public static TheoryData<string> AcceptHeaders => ProblemDetailsTests.AcceptHeaders;

    [Theory]
    [MemberData(nameof(AcceptHeaders))]
    public async Task Unhandled_exception_returns_a_500_problem_without_internal_details(string accept)
    {
        await using var api = TestApi.Create(configureServices: ThrowFromRepository);

        using var response = await SendCreateAsync(api, accept, RouteTemplate);

        await ProblemAssert.HasShapeAsync(response, 500);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(Secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(LeakyTestException), body, StringComparison.Ordinal);
        Assert.DoesNotContain("TalepPazari", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
        AssertSecretInNoLogEntry(api.Logs);
    }

    [Theory]
    [MemberData(nameof(AcceptHeaders))]
    public async Task Unhandled_exception_logs_type_and_route_template_only_and_the_full_exception_at_Debug(string accept)
    {
        await using var api = TestApi.Create(configureServices: ThrowFromRepository);

        using var response = await SendCreateAsync(api, accept, $"{RouteTemplate}?q={RequestData}", name: RequestData);

        Assert.Equal(500, (int)response.StatusCode);
        var entries = api.Logs.Entries.ToList();
        Assert.NotEmpty(entries);
        AssertSecretInNoLogEntry(api.Logs);

        // Query string and body appear neither in Warning+ entries nor in the handler's own entries.
        // (The framework's Information request log prints the URL with its query string; appsettings.json
        // filters Microsoft.AspNetCore below Warning, and the final logging setup is T-008b.)
        Assert.All(
            entries.Where(entry => entry.Level >= LogLevel.Warning || entry.Category.StartsWith("TalepPazari", StringComparison.Ordinal)),
            entry => Assert.DoesNotContain(RequestData, entry.Message, StringComparison.Ordinal));

        Assert.Contains(entries, entry => entry.Exception is LeakyTestException && entry.Level == LogLevel.Debug);

        // One Error entry with the exception type and the route template.
        var error = Assert.Single(entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Contains(typeof(LeakyTestException).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains(RouteTemplate, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exception_after_the_response_started_aborts_the_request_and_logs_type_only()
    {
        var endpoint = new StartedThenThrowsEndpoint();
        await using var api = TestApi.Create(configureServices: services =>
            services.AddSingleton<IStartupFilter>(endpoint));

        // The status line and part of the body are already sent, so no problem details can follow:
        // the request is aborted and the client sees a broken response.
        var clientError = await Record.ExceptionAsync(async () =>
        {
            using var response = await api.Client.GetAsync(StartedThenThrowsPath, TestContext.Current.CancellationToken);
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        });

        // Client side: the response is broken, and not because the in-memory test server passed the server's
        // exception through. The inner exception kind (IOException or OperationCanceledException) varies
        // from run to run, so it is not asserted.
        Assert.NotNull(clientError);
        Assert.DoesNotContain(ExceptionChain(clientError), exception => exception is LeakyTestException);

        // Server side, the actual proof: RequestAborted fired, so the request was aborted, not completed.
        await endpoint.Aborted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var entries = api.Logs.Entries.ToList();
        AssertSecretInNoLogEntry(api.Logs);
        Assert.Contains(entries, entry => entry.Exception is LeakyTestException && entry.Level == LogLevel.Debug);
        var error = Assert.Single(entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Contains(typeof(LeakyTestException).FullName!, error.Message, StringComparison.Ordinal);
    }

    // The exception message appears in no formatted entry of any category, at any level (including
    // Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware and the server), and the exception
    // object travels only with Debug (or lower) entries.
    private static void AssertSecretInNoLogEntry(TestLoggerProvider logs)
    {
        var entries = logs.Entries.ToList();
        Assert.All(entries, entry => Assert.DoesNotContain(Secret, entry.Message, StringComparison.Ordinal));
        Assert.All(entries.Where(entry => entry.Exception is not null), entry => Assert.True(
            entry.Level <= LogLevel.Debug, $"{entry.Category} logged the exception object at {entry.Level}."));
    }

    private static IEnumerable<Exception> ExceptionChain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static async Task<HttpResponseMessage> SendCreateAsync(TestApi api, string accept, string url, string name = "item")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { name }) };
        if (accept != NoAccept)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        return await api.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static void ThrowFromRepository(IServiceCollection services) =>
        services.AddScoped<IExampleItemRepository, ThrowingRepository>();

    private sealed class ThrowingRepository : IExampleItemRepository
    {
        public void Add(ExampleItem item) => throw new LeakyTestException(Secret);
    }

    private sealed class LeakyTestException(string message) : Exception(message);

    // Test host only: a terminal branch after the host's own pipeline (so inside its error handling)
    // that writes and flushes part of a response, then throws. Aborted completes when RequestAborted fires.
    private sealed class StartedThenThrowsEndpoint : IStartupFilter
    {
        public TaskCompletionSource Aborted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(StartedThenThrowsPath, branch => branch.Run(async context =>
            {
                context.RequestAborted.Register(() => Aborted.TrySetResult());
                await context.Response.WriteAsync("partial", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
                throw new LeakyTestException(Secret);
            }));
        };
    }
}
