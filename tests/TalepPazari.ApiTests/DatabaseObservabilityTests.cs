using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using TalepPazari.ApiTests.Support;
using TalepPazari.IntegrationTests.Support;
using TalepPazari.Modules.Example.Infrastructure;

namespace TalepPazari.ApiTests;

// Correlation and log safety of the Api host against real PostgreSQL (per-class fixture: these tests need
// Docker; the rest of the Api tests do not).
public sealed class DatabaseObservabilityTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Header = "X-Correlation-ID";
    private const string ItemsRoute = "/api/v1/example/items";
    private const string MessageSecret = "msg-secret-6d0c-raised-by-postgres";
    private const string DetailSecret = "detail-secret-b27e-raised-by-postgres";
    private const string WrongPassword = "wrong-pw-secret-5a19";

    [Fact]
    public async Task The_same_correlation_id_is_in_the_response_header_a_log_entry_and_the_outbox_row()
    {
        const string correlationId = "corr-api-outbox-1";
        var (api, connectionString) = await CreateApiAsync();
        await using var _ = api;

        using var request = new HttpRequestMessage(HttpMethod.Post, ItemsRoute) { Content = JsonContent.Create(new { name = "item" }) };
        request.Headers.Add(Header, correlationId);
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(201, (int)response.StatusCode);
        Assert.Equal(correlationId, Assert.Single(response.Headers.GetValues(Header)));
        var requestLog = Assert.Single(api.Logs.Entries, entry => entry.Category == "TalepPazari.Api.Observability.RequestLogMiddleware");
        Assert.Equal(correlationId, requestLog.Scope("CorrelationId"));
        Assert.Equal(correlationId, await ScalarAsync<string>(connectionString, "SELECT \"CorrelationId\" FROM example.\"OutboxMessages\""));
    }

    [Fact]
    public async Task A_generated_correlation_id_is_the_one_in_the_outbox_row()
    {
        var (api, connectionString) = await CreateApiAsync();
        await using var _ = api;

        using var response = await api.Client.PostAsJsonAsync(ItemsRoute, new { name = "item" }, TestContext.Current.CancellationToken);

        var generated = Assert.Single(response.Headers.GetValues(Header));
        Assert.Matches("^[0-9a-f]{32}$", generated);
        Assert.Equal(generated, await ScalarAsync<string>(connectionString, "SELECT \"CorrelationId\" FROM example.\"OutboxMessages\""));
    }

    // The item is saved, so EF Core's command log (lowered to Trace) runs with the body secret as a parameter.
    [Fact]
    public async Task A_password_a_token_and_the_body_appear_in_no_entry_at_any_level_when_the_item_is_saved()
    {
        var (api, _) = await CreateApiAsync(RequestLogPolicyTests.EverythingAtTrace);
        await using var __ = api;

        using var request = RequestLogPolicyTests.SecretBearingCreateRequest();
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(201, (int)response.StatusCode);
        Assert.Contains(api.Logs.Entries, entry => entry.Category.StartsWith("Microsoft.EntityFrameworkCore.Database.Command", StringComparison.Ordinal));
        LogAssert.NoTextInAnyEntry(api.Logs.Entries, RequestLogPolicyTests.Password, RequestLogPolicyTests.Token, RequestLogPolicyTests.BodySecret);
    }

    // A trigger makes SaveChanges fail with text from the database (RAISE EXCEPTION ... USING DETAIL).
    // Npgsql redacts DETAIL unless the connection string sets Include Error Detail, so both settings run.
    // EF Core and Npgsql log the failure with the exception (EF's text embeds the message); the log policy
    // moves those to Debug, so no Warning+ entry of any category has the message, the detail or an exception.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Database_error_text_reaches_no_Warning_or_higher_entry(bool includeErrorDetail)
    {
        var (api, connectionString) = await CreateApiAsync(includeErrorDetail: includeErrorDetail);
        await using var _ = api;
        await ExecuteAsync(connectionString, $"""
            CREATE FUNCTION example.reject_item() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'item rejected: %', '{MessageSecret}' USING DETAIL = '{DetailSecret}';
            END $$;
            CREATE TRIGGER reject_item BEFORE INSERT ON example."ExampleItems" FOR EACH ROW EXECUTE FUNCTION example.reject_item();
            """);

        using var response = await api.Client.PostAsJsonAsync(ItemsRoute, new { name = "item" }, TestContext.Current.CancellationToken);

        Assert.Equal(500, (int)response.StatusCode);
        var entries = api.Logs.Entries.ToList();
        var warningOrAbove = entries.Where(entry => entry.Level >= LogLevel.Warning).ToList();
        Assert.All(warningOrAbove, entry =>
        {
            Assert.DoesNotContain(MessageSecret, entry.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain(DetailSecret, entry.AllText, StringComparison.Ordinal);
        });
        LogAssert.NoExceptionAtWarningOrAbove(entries);

        // The failure really carried the text, and EF Core's own Error entry was replaced, not lost.
        Assert.Contains(entries, entry => entry.Level == LogLevel.Debug && entry.AllText.Contains(MessageSecret, StringComparison.Ordinal));
        Assert.Contains(warningOrAbove, entry =>
            entry.Category.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            && entry.Message.Contains("message and exception are logged at Debug only", StringComparison.Ordinal));
        Assert.Equal(
            includeErrorDetail,
            entries.Any(entry => entry.Level == LogLevel.Debug && entry.AllText.Contains(DetailSecret, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task With_a_wrong_password_no_entry_contains_the_password_or_the_connection_string()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var wrong = new NpgsqlConnectionStringBuilder(connectionString) { Password = WrongPassword }.ConnectionString;
        await using var api = TestApi.Create(wrong, settings: RequestLogPolicyTests.EverythingAtTrace);

        using var response = await api.Client.GetAsync(ItemsRoute, TestContext.Current.CancellationToken);

        Assert.Equal(500, (int)response.StatusCode);
        Assert.Contains(api.Logs.Entries, entry => entry.Exception is PostgresException { SqlState: "28P01" });
        LogAssert.NoCredentialInAnyEntry(api.Logs.Entries, WrongPassword);
        LogAssert.NoExceptionAtWarningOrAbove(api.Logs.Entries);
    }

    [Fact]
    public async Task Readiness_is_200_Healthy_when_the_database_runs()
    {
        var (api, _) = await CreateApiAsync();
        await using var __ = api;

        // Only the probe's entries: the migration in the setup logs EF Core's expected history-table error.
        var setupEntries = api.Logs.Entries.Count;
        using var response = await api.Client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(api.Logs.Entries.Skip(setupEntries)
            .Where(entry => entry.Level >= LogLevel.Warning)
            .Select(entry => $"{entry.Category}: {entry.Message}"));
    }

    private async Task<(TestApi Api, string ConnectionString)> CreateApiAsync(
        IReadOnlyDictionary<string, string?>? settings = null, bool includeErrorDetail = false)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        if (includeErrorDetail)
        {
            connectionString = new NpgsqlConnectionStringBuilder(connectionString) { IncludeErrorDetail = true }.ConnectionString;
        }

        var api = TestApi.Create(connectionString, settings: settings);
        try
        {
            await ExampleModule.MigrateDatabaseAsync(api.Services, TestContext.Current.CancellationToken);
        }
        catch
        {
            await api.DisposeAsync();
            throw;
        }

        return (api, connectionString);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
