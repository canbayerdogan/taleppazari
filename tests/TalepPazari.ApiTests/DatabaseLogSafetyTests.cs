using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalepPazari.ApiTests.Support;

namespace TalepPazari.ApiTests;

// What EF Core and Npgsql write through the Api host when the database is unreachable. No database needed:
// the connection string points at a closed port. The wrong-password and failing-SaveChanges cases need
// PostgreSQL and are in DatabaseObservabilityTests.
public sealed class DatabaseLogSafetyTests
{
    public const string DownPassword = "down-pw-secret-3f70";

    private const string DownConnectionString =
        "Host=127.0.0.1;Port=1;Database=down_db;Username=down_user;Password=" + DownPassword + ";Timeout=2";

    [Fact]
    public async Task With_the_database_down_no_entry_contains_the_password_or_the_connection_string()
    {
        await using var api = TestApi.Create(DownConnectionString, settings: RequestLogPolicyTests.EverythingAtTrace);

        using var response = await api.Client.GetAsync("/api/v1/example/items", TestContext.Current.CancellationToken);

        Assert.Equal(500, (int)response.StatusCode);
        LogAssert.NoCredentialInAnyEntry(api.Logs.Entries, DownPassword);
        LogAssert.NoExceptionAtWarningOrAbove(api.Logs.Entries);
    }

    [Fact]
    public async Task Sensitive_data_logging_and_detailed_errors_are_off_for_every_DbContext()
    {
        await using var api = TestApi.Create();
        await using var scope = api.Services.CreateAsyncScope();

        var options = scope.ServiceProvider.GetServices<DbContextOptions>().ToList();

        Assert.NotEmpty(options);
        Assert.All(options, option =>
        {
            var core = option.FindExtension<CoreOptionsExtension>();
            Assert.False(core?.IsSensitiveDataLoggingEnabled ?? false, $"{option.ContextType}: EnableSensitiveDataLogging");
            Assert.False(core?.DetailedErrorsEnabled ?? false, $"{option.ContextType}: EnableDetailedErrors");
        });
    }
}
