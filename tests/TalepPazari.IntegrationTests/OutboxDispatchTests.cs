using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalepPazari.IntegrationTests.Support;
using TalepPazari.Modules.Example.Contracts.IntegrationEvents;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.IntegrationTests;

public sealed class OutboxDispatchTests(PostgresFixture postgres)
{
    private const string ReceiptHandlerName =
        "TalepPazari.Modules.Example.Application.ExampleItems.RecordExampleItemCreatedReceipt.RecordExampleItemCreatedReceiptHandler";

    // LastError for a handler failure: the exception type, nothing else.
    private const string ExceptionTypeOnly = "System.InvalidOperationException";

    [Fact]
    public async Task A_created_item_is_delivered_once_and_marked_processed()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        var itemId = await host.CreateItemAsync();

        var result = await host.RunOnceAsync();

        Assert.Equal(new(1, 0), result);
        Assert.Equal(itemId, await host.ScalarAsync<Guid>("SELECT \"ItemId\" FROM example.\"ExampleItemCreatedReceipts\""));
        Assert.Equal(ReceiptHandlerName, await host.ScalarAsync<string>("SELECT \"HandlerName\" FROM example.\"InboxMessages\""));
        var row = await host.SingleOutboxRowAsync();
        Assert.Equal(host.Clock.GetUtcNow(), row.ProcessedAt);
        Assert.Null(row.LockedBy);
        Assert.Equal(1, row.Attempts);

        Assert.Equal(new(0, 0), await host.RunOnceAsync());
    }

    [Fact]
    public async Task A_redelivered_message_has_no_second_effect()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        await host.CreateItemAsync();
        await host.RunOnceAsync();

        // Make the processed message due again, as an at-least-once redelivery would.
        await host.ExecuteAsync("UPDATE example.\"OutboxMessages\" SET \"ProcessedAt\" = NULL");
        var result = await host.RunOnceAsync();

        Assert.Equal(new(1, 0), result);
        Assert.Equal(1, await host.CountAsync("ExampleItemCreatedReceipts"));
        Assert.Equal(1, await host.CountAsync("InboxMessages"));
        Assert.NotNull((await host.SingleOutboxRowAsync()).ProcessedAt);
    }

    [Fact]
    public async Task A_failing_handler_is_retried_with_backoff_then_dead_lettered()
    {
        await using var host = await ExampleTestHost.CreateAsync(
            postgres, services => services.AddScoped<IIntegrationEventHandler<ExampleItemCreatedV1>, ThrowingHandler>());
        await host.CreateItemAsync();

        // Attempt 1 of 3 fails; next attempt after RetryBaseDelay (1 s).
        await host.RunOnceAsync();
        var row = await host.SingleOutboxRowAsync();
        Assert.Equal(1, row.Attempts);
        Assert.Equal(host.Clock.GetUtcNow().AddSeconds(1), row.NextAttemptAt);
        Assert.Null(row.LockedBy);
        Assert.Equal(ExceptionTypeOnly, row.LastError);

        // Not due yet: nothing is claimed.
        Assert.Equal(new(0, 0), await host.RunOnceAsync());

        // Attempt 2 fails; the delay doubles to 2 s.
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        await host.RunOnceAsync();
        row = await host.SingleOutboxRowAsync();
        Assert.Equal(2, row.Attempts);
        Assert.Equal(host.Clock.GetUtcNow().AddSeconds(2), row.NextAttemptAt);

        // Attempt 3 reaches MaxAttempts: dead-lettered.
        host.Clock.Advance(TimeSpan.FromSeconds(2));
        await host.RunOnceAsync();
        row = await host.SingleOutboxRowAsync();
        Assert.Equal(3, row.Attempts);
        Assert.Equal(host.Clock.GetUtcNow(), row.DeadLetteredAt);
        Assert.Null(row.ProcessedAt);
        Assert.Equal(ExceptionTypeOnly, row.LastError);

        // Dead-lettered for all handlers; the receipt handler succeeded once and the inbox kept it at one.
        host.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(new(0, 0), await host.RunOnceAsync());
        Assert.Equal(1, await host.CountAsync("ExampleItemCreatedReceipts"));
    }

    [Fact]
    public async Task A_handler_exception_message_never_reaches_LastError_or_warning_logs()
    {
        await using var host = await ExampleTestHost.CreateAsync(
            postgres, services => services.AddScoped<IIntegrationEventHandler<ExampleItemCreatedV1>, ThrowingHandler>());
        var itemId = (await host.CreateItemAsync()).ToString();

        // Attempt 1 is retried, attempts 2 and 3 end in a dead-letter: both Warning and Error paths run.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await host.RunOnceAsync();
            host.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var row = await host.SingleOutboxRowAsync();
        Assert.NotNull(row.DeadLetteredAt);
        Assert.Equal(ExceptionTypeOnly, row.LastError);

        var entries = host.Logs.Entries.ToList();

        // Not vacuous: the id is in the handler's exception message, and that exception is logged at Debug.
        Assert.Contains(entries, entry =>
            entry.Level == LogLevel.Debug && entry.Exception?.Message.Contains(itemId, StringComparison.Ordinal) == true);

        var warnings = entries.Where(entry => entry.Level >= LogLevel.Warning).ToList();
        Assert.Contains(warnings, entry => entry.Level == LogLevel.Warning && entry.Message.Contains(ExceptionTypeOnly, StringComparison.Ordinal));
        Assert.Contains(warnings, entry => entry.Level == LogLevel.Error && entry.Message.Contains(ExceptionTypeOnly, StringComparison.Ordinal));
        Assert.All(warnings, entry =>
        {
            Assert.DoesNotContain(itemId, entry.Message, StringComparison.Ordinal);
            Assert.Null(entry.Exception);
        });
    }

    [Fact]
    public async Task A_failing_decorated_handler_leaves_no_inbox_row_and_no_effect_then_a_retry_applies_it_once()
    {
        // The real receipt handler behind the real inbox decorator. The trigger makes the handler's
        // SaveChanges fail inside the decorator's transaction (the handler code itself does not throw).
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        await host.CreateItemAsync();
        await host.ExecuteAsync("""
            CREATE FUNCTION example.fail_insert() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN RAISE EXCEPTION 'simulated receipt failure'; END $$;
            CREATE TRIGGER fail_insert BEFORE INSERT ON example."ExampleItemCreatedReceipts"
            FOR EACH ROW EXECUTE FUNCTION example.fail_insert();
            """);

        Assert.Equal(new(1, 0), await host.RunOnceAsync());

        Assert.Equal(0, await host.CountAsync("InboxMessages"));
        Assert.Equal(0, await host.CountAsync("ExampleItemCreatedReceipts"));
        var row = await host.SingleOutboxRowAsync();
        Assert.Null(row.ProcessedAt);
        Assert.Null(row.DeadLetteredAt);
        Assert.Equal(1, row.Attempts);

        // The cause is gone; the retry applies the effect once.
        await host.ExecuteAsync("DROP TRIGGER fail_insert ON example.\"ExampleItemCreatedReceipts\"");
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(new(1, 0), await host.RunOnceAsync());
        Assert.Equal(1, await host.CountAsync("InboxMessages"));
        Assert.Equal(1, await host.CountAsync("ExampleItemCreatedReceipts"));
        Assert.NotNull((await host.SingleOutboxRowAsync()).ProcessedAt);

        // A further redelivery has no second effect.
        await host.ExecuteAsync("UPDATE example.\"OutboxMessages\" SET \"ProcessedAt\" = NULL");
        Assert.Equal(new(1, 0), await host.RunOnceAsync());
        Assert.Equal(1, await host.CountAsync("InboxMessages"));
        Assert.Equal(1, await host.CountAsync("ExampleItemCreatedReceipts"));
    }

    [Fact]
    public async Task The_inbox_decorator_refuses_a_message_without_an_id()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        await using var scope = host.Services.CreateAsyncScope();

        // Resolved outside the dispatcher, so the event context was never set (MessageId is Guid.Empty).
        var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<ExampleItemCreatedV1>>();
        var integrationEvent = new ExampleItemCreatedV1(Guid.CreateVersion7(), host.Clock.GetUtcNow());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(integrationEvent, TestContext.Current.CancellationToken));

        Assert.Equal(0, await host.CountAsync("InboxMessages"));
        Assert.Equal(0, await host.CountAsync("ExampleItemCreatedReceipts"));
    }

    // Mark* must change nothing once a row is processed or dead-lettered, even for the token that claimed it.
    [Theory]
    [InlineData("ProcessedAt")]
    [InlineData("DeadLetteredAt")]
    public async Task Mark_statements_do_not_touch_a_finished_row_even_with_the_claim_token(string finishedColumn)
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        await host.CreateItemAsync();
        var claimToken = Guid.CreateVersion7();
        Assert.Single(await host.ClaimAsync(claimToken, 10));

        // A state no code path produces today: finished, but still locked by this token.
        await host.ExecuteAsync($"UPDATE example.\"OutboxMessages\" SET \"{finishedColumn}\" = '2026-01-01T00:00:00Z'");
        var before = await host.SingleOutboxRowAsync();
        Assert.Equal(claimToken, before.LockedBy);

        var id = before.Id;
        var now = host.Clock.GetUtcNow();
        var token = TestContext.Current.CancellationToken;
        Assert.False(await host.WithStoreAsync(store => store.MarkProcessedAsync(id, claimToken, now, token)));
        Assert.False(await host.WithStoreAsync(store => store.MarkFailedAsync(id, claimToken, "error", now, token)));
        Assert.False(await host.WithStoreAsync(store => store.MarkDeadLetteredAsync(id, claimToken, "error", now, token)));

        Assert.Equal(before, await host.SingleOutboxRowAsync());
    }

    [Fact]
    public async Task A_message_that_never_completes_is_dead_lettered_after_MaxAttempts_claims()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        await host.CreateItemAsync();

        // Three claims that never finish, as if the process crashed while handling each time.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Single(await host.ClaimAsync(Guid.CreateVersion7(), 10));
            host.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        await host.RunOnceAsync();

        var row = await host.SingleOutboxRowAsync();
        Assert.Equal(4, row.Attempts);
        Assert.NotNull(row.DeadLetteredAt);
        Assert.StartsWith("Claimed 4 times without completing", row.LastError, StringComparison.Ordinal);
        Assert.Equal(0, await host.CountAsync("ExampleItemCreatedReceipts"));
    }

    [Fact]
    public async Task A_crash_between_handling_and_acknowledgement_does_not_lose_the_event()
    {
        await using var crashing = await ExampleTestHost.CreateAsync(postgres, CrashBeforeAcknowledgementStore.Install);
        await crashing.CreateItemAsync();

        // Instance A handles the message (receipt committed) and dies before the acknowledgement.
        Assert.Equal(new(0, 1), await crashing.RunOnceAsync());
        var row = await crashing.SingleOutboxRowAsync();
        Assert.Null(row.ProcessedAt);
        Assert.NotNull(row.LockedBy);
        Assert.Equal(1, await crashing.CountAsync("ExampleItemCreatedReceipts"));

        // Instance B cannot take the message while A's lease runs.
        await using var survivor = crashing.AnotherInstance();
        Assert.Equal(new(0, 0), await survivor.RunOnceAsync());

        // After the lease expires B redelivers it; the inbox prevents a second receipt.
        survivor.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(new(1, 0), await survivor.RunOnceAsync());
        row = await survivor.SingleOutboxRowAsync();
        Assert.NotNull(row.ProcessedAt);
        Assert.Equal(2, row.Attempts);
        Assert.Equal(1, await survivor.CountAsync("ExampleItemCreatedReceipts"));
        Assert.Equal(1, await survivor.CountAsync("InboxMessages"));
    }

    [Fact]
    public async Task An_unknown_event_type_is_dead_lettered()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);
        await InsertOutboxRowAsync(host, "TalepPazari.Modules.Unknown.Contracts.IntegrationEvents.SomethingHappenedV1");

        await host.RunOnceAsync();

        var row = await host.SingleOutboxRowAsync();
        Assert.NotNull(row.DeadLetteredAt);
        Assert.StartsWith("Unknown integration event type", row.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_registered_event_type_without_handlers_is_marked_processed()
    {
        await using var host = await ExampleTestHost.CreateAsync(
            postgres, services => services.AddSingleton<IIntegrationEventTypes, TestEventTypes>());
        await InsertOutboxRowAsync(host, typeof(UnhandledTestEventV1).FullName!);

        Assert.Equal(new(1, 0), await host.RunOnceAsync());

        var row = await host.SingleOutboxRowAsync();
        Assert.NotNull(row.ProcessedAt);
        Assert.Null(row.DeadLetteredAt);
    }

    [Fact]
    public async Task A_shutdown_during_handling_still_records_the_result()
    {
        using var shutdown = new ShutdownSignal();
        await using var host = await ExampleTestHost.CreateAsync(postgres, services =>
        {
            services.AddSingleton(shutdown);
            services.AddScoped<IIntegrationEventHandler<ExampleItemCreatedV1>, CancellingHandler>();
        });
        await host.CreateItemAsync();

        await host.Dispatcher.RunOnceAsync(shutdown.Source.Token);

        Assert.True(shutdown.Source.IsCancellationRequested);
        Assert.NotNull((await host.SingleOutboxRowAsync()).ProcessedAt);
    }

    private static Task InsertOutboxRowAsync(ExampleTestHost host, string type) => host.ExecuteAsync($"""
        INSERT INTO example."OutboxMessages" ("Id", "Type", "Payload", "OccurredAt", "Attempts", "NextAttemptAt")
        VALUES ('{Guid.CreateVersion7()}', '{type}', '{"{"}"id":"{Guid.CreateVersion7()}"{"}"}',
                '{host.Clock.GetUtcNow():O}', 0, '{host.Clock.GetUtcNow():O}')
        """);
}
