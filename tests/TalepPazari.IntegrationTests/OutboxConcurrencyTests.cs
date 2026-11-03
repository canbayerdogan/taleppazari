using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TalepPazari.IntegrationTests.Support;
using TalepPazari.Modules.Example.Contracts.IntegrationEvents;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.IntegrationTests;

// Two dispatcher instances on one database never process the same message concurrently.
public sealed class OutboxConcurrencyTests(PostgresFixture postgres)
{
    private const int MessageCount = 100;

    [Fact]
    public async Task Concurrent_claims_return_disjoint_messages()
    {
        await using var first = await ExampleTestHost.CreateAsync(postgres);
        await using var second = first.AnotherInstance();
        await CreateItemsAsync(first);

        var claims = await Task.WhenAll(
            first.ClaimAsync(Guid.CreateVersion7(), 60),
            second.ClaimAsync(Guid.CreateVersion7(), 60));

        var firstIds = claims[0].Select(message => message.Id).ToHashSet();
        var secondIds = claims[1].Select(message => message.Id).ToHashSet();
        Assert.Empty(firstIds.Intersect(secondIds));
        Assert.Equal(MessageCount, firstIds.Count + secondIds.Count);
    }

    [Fact]
    public async Task Two_dispatchers_deliver_every_message_exactly_once()
    {
        var log = new DeliveryLog();
        void AddRecorder(IServiceCollection services)
        {
            services.AddSingleton(log);
            services.AddScoped<IIntegrationEventHandler<ExampleItemCreatedV1>, RecordingHandler>();
        }

        var settings = new Dictionary<string, string?>(ExampleTestHost.DefaultOutboxSettings) { ["Outbox:BatchSize"] = "10" };
        await using var first = await ExampleTestHost.CreateAsync(postgres, AddRecorder, settings);
        await using var second = ExampleTestHost.Create(first.ConnectionString, first.Clock, AddRecorder, settings);
        await CreateItemsAsync(first);

        // Whether both dispatchers get work is timing-dependent and not asserted; row skipping under a
        // held lock is proven deterministically by A_claim_skips_rows_locked_by_another_transaction_without_waiting.
        await Task.WhenAll(DrainAsync(first), DrainAsync(second));

        // The recording handler has no inbox, so any concurrent or repeated delivery would show up here.
        Assert.Equal(MessageCount, log.Deliveries.Count);
        Assert.Equal(MessageCount, log.Deliveries.Distinct().Count());
        Assert.Equal(MessageCount, await first.CountAsync("ExampleItemCreatedReceipts"));
        Assert.Equal(MessageCount, await first.ScalarAsync<long>(
            "SELECT count(*) FROM example.\"OutboxMessages\" WHERE \"ProcessedAt\" IS NOT NULL AND \"Attempts\" = 1"));
    }

    [Fact]
    public async Task A_claim_skips_rows_locked_by_another_transaction_without_waiting()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        for (var index = 0; index < 20; index++)
        {
            await host.CreateItemAsync($"item {index}");
        }

        var allIds = await ReadIdsAsync(host.ConnectionString, "SELECT \"Id\" FROM example.\"OutboxMessages\"", transaction: null);
        HashSet<Guid> lockedIds;
        HashSet<Guid> firstClaim;

        // Another transaction (for example, a dispatcher in the middle of its claim) holds row locks on 5 due rows.
        var locker = new NpgsqlConnection(host.ConnectionString);
        await locker.OpenAsync(TestContext.Current.CancellationToken);
        var lockTransaction = await locker.BeginTransactionAsync(TestContext.Current.CancellationToken);
        try
        {
            lockedIds = await ReadIdsAsync(
                locker, "SELECT \"Id\" FROM example.\"OutboxMessages\" ORDER BY \"Id\" LIMIT 5 FOR UPDATE", lockTransaction);
            Assert.Equal(5, lockedIds.Count);

            // Without SKIP LOCKED this claim would block on the locked rows until the timeout cancels it.
            using var noWaiting = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            noWaiting.CancelAfter(TimeSpan.FromSeconds(5));
            firstClaim = (await host.ClaimAsync(Guid.CreateVersion7(), 100, noWaiting.Token)).Select(message => message.Id).ToHashSet();
        }
        finally
        {
            await lockTransaction.RollbackAsync(TestContext.Current.CancellationToken);
            await lockTransaction.DisposeAsync();
            await locker.DisposeAsync();
        }

        Assert.Equal(allIds.Except(lockedIds).ToHashSet(), firstClaim);

        // Released: the skipped rows were claim-eligible all along, and now exactly they are claimed.
        var secondClaim = (await host.ClaimAsync(Guid.CreateVersion7(), 100)).Select(message => message.Id).ToHashSet();
        Assert.Equal(lockedIds, secondClaim);
    }

    private static async Task<HashSet<Guid>> ReadIdsAsync(string connectionString, string sql, NpgsqlTransaction? transaction)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return await ReadIdsAsync(connection, sql, transaction);
    }

    private static async Task<HashSet<Guid>> ReadIdsAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var ids = new HashSet<Guid>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static async Task CreateItemsAsync(ExampleTestHost host)
    {
        for (var index = 0; index < MessageCount; index++)
        {
            await host.CreateItemAsync($"item {index}");
        }
    }

    // Runs passes until one claims nothing; returns the number of messages this dispatcher claimed.
    private static async Task<int> DrainAsync(ExampleTestHost host)
    {
        var total = 0;
        while (true)
        {
            var result = await host.RunOnceAsync();
            Assert.Equal(0, result.FailedStores);
            if (result.Claimed == 0)
            {
                return total;
            }

            total += result.Claimed;
        }
    }
}
