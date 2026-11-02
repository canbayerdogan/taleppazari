using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;
using TalepPazari.Modules.Example.Infrastructure;
using TalepPazari.SharedKernel.Messaging;
using TalepPazari.Worker.Outbox;

namespace TalepPazari.IntegrationTests.Support;

// The Example module and the outbox dispatcher, composed the way the Worker host composes them,
// on one test database. Several hosts on one database act as several Worker instances.
public sealed class ExampleTestHost : IAsyncDisposable
{
    public static readonly IReadOnlyDictionary<string, string?> DefaultOutboxSettings = new Dictionary<string, string?>
    {
        ["Outbox:BatchSize"] = "20",
        ["Outbox:LeaseDuration"] = "00:01:00",
        ["Outbox:MaxAttempts"] = "3",
        ["Outbox:RetryBaseDelay"] = "00:00:01",
        ["Outbox:RetryMaxDelay"] = "00:00:10",
    };

    private ExampleTestHost(string connectionString, ManualTimeProvider clock, ServiceProvider services)
    {
        ConnectionString = connectionString;
        Clock = clock;
        Services = services;
    }

    public string ConnectionString { get; }

    public ManualTimeProvider Clock { get; }

    public ServiceProvider Services { get; }

    public OutboxDispatcher Dispatcher => Services.GetRequiredService<OutboxDispatcher>();

    // A fresh, migrated database and a first host on it.
    public static async Task<ExampleTestHost> CreateAsync(
        PostgresFixture postgres, Action<IServiceCollection>? configure = null, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var host = Create(connectionString, new ManualTimeProvider(), configure, settings);
        await ExampleModule.MigrateDatabaseAsync(host.Services, TestContext.Current.CancellationToken);
        return host;
    }

    public static ExampleTestHost Create(
        string connectionString,
        ManualTimeProvider clock,
        Action<IServiceCollection>? configure = null,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var values = new Dictionary<string, string?>(settings ?? DefaultOutboxSettings)
        {
            ["ConnectionStrings:Default"] = connectionString,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddExampleModule(configuration);
        services.AddOutboxDispatcher(configuration);
        configure?.Invoke(services);

        return new ExampleTestHost(
            connectionString, clock, services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }));
    }

    // A second "Worker instance": same database and clock, its own services.
    public ExampleTestHost AnotherInstance(Action<IServiceCollection>? configure = null) => Create(ConnectionString, Clock, configure);

    public async Task<Guid> CreateItemAsync(string name = "item")
    {
        await using var scope = Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateExampleItemHandler>();
        var result = await handler.HandleAsync(new CreateExampleItemCommand(name), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        return result.Item.Id;
    }

    public Task<OutboxDispatchResult> RunOnceAsync() => Dispatcher.RunOnceAsync(TestContext.Current.CancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(Guid claimToken, int batchSize)
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetServices<IOutboxStore>().Single();
        var now = Clock.GetUtcNow();
        return await store.ClaimAsync(claimToken, batchSize, now, now + TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public async Task<OutboxRow> SingleOutboxRowAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT "Id", "Type", "Payload"::text, "Attempts", "NextAttemptAt", "LockedBy", "ProcessedAt", "DeadLetteredAt", "LastError"
            FROM example."OutboxMessages"
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), "no outbox row");
        var row = new OutboxRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetString(8));
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken), "more than one outbox row");
        return row;
    }

    public Task<long> CountAsync(string table) => ScalarAsync<long>($"SELECT count(*) FROM example.\"{table}\"");

    public async ValueTask DisposeAsync() => await Services.DisposeAsync();
}

public sealed record OutboxRow(
    Guid Id,
    string Type,
    string Payload,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    Guid? LockedBy,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset? DeadLetteredAt,
    string? LastError);
