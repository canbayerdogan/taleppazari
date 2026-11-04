using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TalepPazari.IntegrationTests.Support;
using TalepPazari.Modules.Example.Infrastructure;

namespace TalepPazari.IntegrationTests;

// The log policy in the real Worker composition: no Warning+ entry of any category (EF Core, Npgsql, the
// dispatcher) carries an exception object, the exception message or database error text, and no entry at
// any level contains a password or a connection string.
public sealed class WorkerLogPolicyTests(PostgresFixture postgres)
{
    private const string MessageSecret = "msg-secret-1c8e-raised-by-postgres";
    private const string DetailSecret = "detail-secret-77a4-raised-by-postgres";
    private const string WrongPassword = "wrong-pw-secret-e602";
    private const string DownPassword = "down-pw-secret-90b3";

    // A trigger makes the receipt handler's SaveChanges fail with text from the database.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Database_error_text_from_a_failing_handler_reaches_no_Warning_or_higher_entry(bool includeErrorDetail)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(await postgres.CreateDatabaseAsync())
        {
            IncludeErrorDetail = includeErrorDetail,
        }.ConnectionString;
        await using var worker = WorkerTestHost.Create(connectionString);
        await ExampleModule.MigrateDatabaseAsync(worker.Services, TestContext.Current.CancellationToken);
        await worker.CreateItemAsync();
        await ExecuteAsync(connectionString, $"""
            CREATE FUNCTION example.reject_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'receipt rejected: %', '{MessageSecret}' USING DETAIL = '{DetailSecret}';
            END $$;
            CREATE TRIGGER reject_receipt BEFORE INSERT ON example."ExampleItemCreatedReceipts"
                FOR EACH ROW EXECUTE FUNCTION example.reject_receipt();
            """);

        var result = await worker.RunOnceAsync();

        Assert.Equal(new(1, 0), result);
        var entries = worker.Logs.Entries.ToList();
        var warningOrAbove = entries.Where(entry => entry.Level >= LogLevel.Warning).ToList();
        Assert.All(warningOrAbove, entry =>
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(MessageSecret, entry.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain(DetailSecret, entry.AllText, StringComparison.Ordinal);
        });

        // The dispatcher's own Warning (type only) and EF Core's replaced Error entry are there.
        Assert.Contains(warningOrAbove, entry => entry.Category == "TalepPazari.Worker.Outbox.OutboxDispatcher");
        Assert.Contains(warningOrAbove, entry =>
            entry.Category.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            && entry.Message.Contains("message and exception are logged at Debug only", StringComparison.Ordinal));
        Assert.Contains(entries, entry => entry.Level == LogLevel.Debug && entry.AllText.Contains(MessageSecret, StringComparison.Ordinal));
        Assert.Equal(
            includeErrorDetail,
            entries.Any(entry => entry.Level == LogLevel.Debug && entry.AllText.Contains(DetailSecret, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task With_a_wrong_password_no_entry_contains_the_password_or_the_connection_string()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(await postgres.CreateDatabaseAsync())
        {
            Password = WrongPassword,
        }.ConnectionString;
        await using var worker = WorkerTestHost.Create(connectionString);

        var result = await worker.RunOnceAsync();

        Assert.Equal(1, result.FailedStores);
        Assert.Contains(worker.Logs.Entries, entry => entry.Exception is PostgresException { SqlState: "28P01" });
        AssertNoCredential(worker.Logs, WrongPassword);
    }

    [Fact]
    public async Task With_the_database_down_no_entry_contains_the_password_or_the_connection_string()
    {
        await using var worker = WorkerTestHost.Create(
            $"Host=127.0.0.1;Port=1;Database=down_db;Username=down_user;Password={DownPassword};Timeout=2");

        var result = await worker.RunOnceAsync();

        Assert.Equal(1, result.FailedStores);
        AssertNoCredential(worker.Logs, DownPassword);
    }

    [Fact]
    public async Task Sensitive_data_logging_and_detailed_errors_are_off_for_every_DbContext()
    {
        await using var worker = WorkerTestHost.Create("Host=127.0.0.1;Port=1;Database=never_opened;Username=u;Password=p");
        await using var scope = worker.Services.CreateAsyncScope();

        var options = scope.ServiceProvider.GetServices<DbContextOptions>().ToList();

        Assert.NotEmpty(options);
        Assert.All(options, option =>
        {
            var core = option.FindExtension<CoreOptionsExtension>();
            Assert.False(core?.IsSensitiveDataLoggingEnabled ?? false, $"{option.ContextType}: EnableSensitiveDataLogging");
            Assert.False(core?.DetailedErrorsEnabled ?? false, $"{option.ContextType}: EnableDetailedErrors");
        });
    }

    private static void AssertNoCredential(TestLoggerProvider logs, string password)
    {
        var entries = logs.Entries.ToList();
        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            Assert.DoesNotContain(password, entry.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain("Password=", entry.AllText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Username=", entry.AllText, StringComparison.OrdinalIgnoreCase);
        });
        Assert.All(entries.Where(entry => entry.Level >= LogLevel.Warning), entry => Assert.Null(entry.Exception));
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
