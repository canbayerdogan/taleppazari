using Microsoft.Extensions.DependencyInjection;
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

        var passes = await Task.WhenAll(DrainAsync(first), DrainAsync(second));

        // The recording handler has no inbox, so any concurrent or repeated delivery would show up here.
        Assert.Equal(MessageCount, log.Deliveries.Count);
        Assert.Equal(MessageCount, log.Deliveries.Distinct().Count());
        Assert.Equal(MessageCount, await first.CountAsync("ExampleItemCreatedReceipts"));
        Assert.Equal(MessageCount, await first.ScalarAsync<long>(
            "SELECT count(*) FROM example.\"OutboxMessages\" WHERE \"ProcessedAt\" IS NOT NULL AND \"Attempts\" = 1"));
        Assert.All(passes, claimed => Assert.True(claimed > 0, "both dispatchers took part"));
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
