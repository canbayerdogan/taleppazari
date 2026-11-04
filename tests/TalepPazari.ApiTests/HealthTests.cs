using Microsoft.Extensions.Logging;
using TalepPazari.ApiTests.Support;

namespace TalepPazari.ApiTests;

// /health/live (process only) and /health/ready (every module's registered check) with the database down.
// No database needed: the connection string points at a closed port. Readiness against a running database
// is in DatabaseObservabilityTests.
public sealed class HealthTests
{
    private const string DownPassword = "health-pw-secret-2b6f";

    // Timeout=1 keeps each failing probe short; the check's own limit is 3 s.
    private const string DownConnectionString =
        "Host=127.0.0.1;Port=1;Database=health_db;Username=health_user;Password=" + DownPassword + ";Timeout=1";

    [Fact]
    public async Task Liveness_is_200_Healthy_with_the_database_down()
    {
        await using var api = TestApi.Create(DownConnectionString);

        using var response = await api.Client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(api.Logs.Entries, entry => entry.Category.Contains("HealthCheck", StringComparison.Ordinal) && entry.Level >= LogLevel.Warning);
    }

    // A plain-text 503 with the status only: the documented exception to the problem-details rule.
    [Fact]
    public async Task Readiness_is_503_Unhealthy_as_plain_text_with_the_database_down()
    {
        await using var api = TestApi.Create(DownConnectionString);

        using var response = await api.Client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // Probes repeat (an orchestrator calls readiness every few seconds): one Warning for the state change,
    // nothing per probe at Warning or above, no exception object, no credential at any level.
    [Fact]
    public async Task Repeated_failing_readiness_probes_write_one_Warning_and_no_credential()
    {
        const int probes = 4;
        await using var api = TestApi.Create(DownConnectionString, settings: RequestLogPolicyTests.EverythingAtTrace);

        for (var probe = 0; probe < probes; probe++)
        {
            using var response = await api.Client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
            Assert.Equal(503, (int)response.StatusCode);
        }

        var entries = api.Logs.Entries.ToList();
        var warning = Assert.Single(entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Equal("TalepPazari.Modules.Example.Infrastructure.Health.ExampleDatabaseHealthCheck", warning.Category);
        Assert.Contains("became unreachable", warning.Message, StringComparison.Ordinal);
        Assert.Equal(probes, entries.Count(entry =>
            entry.Category == "TalepPazari.Api.Observability.RequestLogMiddleware" && entry.Message.Contains("responded 503", StringComparison.Ordinal)));
        LogAssert.NoExceptionAtWarningOrAbove(entries);
        LogAssert.NoCredentialInAnyEntry(entries, DownPassword);
    }
}
