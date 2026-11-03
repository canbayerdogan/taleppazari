using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalepPazari.ApiTests.Support;
using TalepPazari.Modules.Example.Application.ExampleItems;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.ApiTests;

// The failing piece exists only in this test host: a repository that throws. No database needed.
public sealed class UnhandledExceptionTests
{
    private const string Secret = "secret-7c1e-payload-from-the-request";
    private const string RequestData = "request-data-3b9d";
    private const string RouteTemplate = "/api/v1/example/items";

    [Fact]
    public async Task Unhandled_exception_returns_a_500_problem_without_internal_details()
    {
        await using var api = TestApi.Create(configureServices: services =>
            services.AddScoped<IExampleItemRepository, ThrowingRepository>());

        var response = await api.Client.PostAsJsonAsync(RouteTemplate, new { name = "item" }, TestContext.Current.CancellationToken);

        await ProblemAssert.HasShapeAsync(response, 500);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(Secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(LeakyTestException), body, StringComparison.Ordinal);
        Assert.DoesNotContain("TalepPazari", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unhandled_exception_logs_type_and_route_template_only_and_the_full_exception_at_Debug()
    {
        await using var api = TestApi.Create(configureServices: services =>
            services.AddScoped<IExampleItemRepository, ThrowingRepository>());

        await api.Client.PostAsJsonAsync(
            $"{RouteTemplate}?q={RequestData}", new { name = RequestData }, TestContext.Current.CancellationToken);

        var entries = api.Logs.Entries.ToList();
        Assert.NotEmpty(entries);

        // The exception message appears in no formatted entry of any category, at any level, including
        // Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware.
        Assert.All(entries, entry => Assert.DoesNotContain(Secret, entry.Message, StringComparison.Ordinal));

        // Query string and body appear neither in Warning+ entries nor in the handler's own entries.
        // (The framework's Information request log prints the URL with its query string; appsettings.json
        // filters Microsoft.AspNetCore below Warning, and the final logging setup is T-008b.)
        Assert.All(
            entries.Where(entry => entry.Level >= LogLevel.Warning || entry.Category.StartsWith("TalepPazari", StringComparison.Ordinal)),
            entry => Assert.DoesNotContain(RequestData, entry.Message, StringComparison.Ordinal));

        // The exception object travels only with Debug (or lower) entries.
        Assert.All(entries.Where(entry => entry.Exception is not null), entry => Assert.True(
            entry.Level <= LogLevel.Debug, $"{entry.Category} logged the exception object at {entry.Level}."));
        Assert.Contains(entries, entry => entry.Exception is LeakyTestException && entry.Level == LogLevel.Debug);

        // One Error entry with the exception type and the route template.
        var error = Assert.Single(entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Contains(typeof(LeakyTestException).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains(RouteTemplate, error.Message, StringComparison.Ordinal);
    }

    private sealed class ThrowingRepository : IExampleItemRepository
    {
        public void Add(ExampleItem item) => throw new LeakyTestException(Secret);
    }

    private sealed class LeakyTestException(string message) : Exception(message);
}
