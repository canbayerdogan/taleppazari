using TalepPazari.IntegrationTests.Support;
using TalepPazari.Modules.Example.Contracts.IntegrationEvents;

namespace TalepPazari.IntegrationTests;

// The event exists if and only if the state change committed.
public sealed class OutboxAtomicityTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Creating_an_item_writes_the_item_and_its_event_in_one_commit()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);

        var itemId = await host.CreateItemAsync();

        Assert.Equal(1, await host.CountAsync("ExampleItems"));
        var row = await host.SingleOutboxRowAsync();
        Assert.Equal(typeof(ExampleItemCreatedV1).FullName, row.Type);
        Assert.Contains(itemId.ToString(), row.Payload, StringComparison.Ordinal);
        Assert.Equal(0, row.Attempts);
        Assert.Null(row.ProcessedAt);
    }

    [Fact]
    public async Task When_the_outbox_insert_fails_the_item_is_not_saved()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        await host.ExecuteAsync(FailingInsertTrigger("OutboxMessages"));

        await Assert.ThrowsAnyAsync<Exception>(() => host.CreateItemAsync());

        Assert.Equal(0, await host.CountAsync("ExampleItems"));
        Assert.Equal(0, await host.CountAsync("OutboxMessages"));
    }

    [Fact]
    public async Task When_the_item_insert_fails_no_event_is_saved()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        await host.ExecuteAsync(FailingInsertTrigger("ExampleItems"));

        await Assert.ThrowsAnyAsync<Exception>(() => host.CreateItemAsync());

        Assert.Equal(0, await host.CountAsync("ExampleItems"));
        Assert.Equal(0, await host.CountAsync("OutboxMessages"));
    }

    // Test-only trigger that makes every insert into one table fail inside the transaction.
    private static string FailingInsertTrigger(string table) => $"""
        CREATE FUNCTION example.fail_insert() RETURNS trigger LANGUAGE plpgsql AS
        $$ BEGIN RAISE EXCEPTION 'simulated insert failure'; END $$;
        CREATE TRIGGER fail_insert BEFORE INSERT ON example."{table}"
        FOR EACH ROW EXECUTE FUNCTION example.fail_insert();
        """;
}
