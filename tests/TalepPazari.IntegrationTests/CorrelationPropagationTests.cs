using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalepPazari.IntegrationTests.Support;
using TalepPazari.Modules.Example.Application;
using TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;
using TalepPazari.Modules.Example.Contracts.IntegrationEvents;
using TalepPazari.Modules.Example.Infrastructure;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.IntegrationTests;

// The Worker restores a message's correlation ID for its handlers: in their log entries and in the events
// they publish. Without an ambient value the outbox writes null.
public sealed partial class CorrelationPropagationTests(PostgresFixture postgres)
{
    private const string CorrelationId = "corr-worker-handler-1";

    [Fact]
    public async Task Without_an_ambient_correlation_id_the_outbox_row_has_none()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres);

        await host.CreateItemAsync();

        Assert.Equal(DBNull.Value, await host.ScalarAsync<object>("SELECT \"CorrelationId\" FROM example.\"OutboxMessages\""));
    }

    [Fact]
    public async Task A_handler_logs_the_message_correlation_id_and_an_event_it_publishes_carries_it()
    {
        await using var host = await ExampleTestHost.CreateAsync(postgres, services =>
            services.AddScoped<IIntegrationEventHandler<ExampleItemCreatedV1>, RepublishingHandler>());
        var itemId = await host.CreateItemAsync();
        await host.ExecuteAsync($"UPDATE example.\"OutboxMessages\" SET \"CorrelationId\" = '{CorrelationId}'");

        await host.RunOnceAsync();

        var handled = Assert.Single(host.Logs.Entries, entry => entry.Category.EndsWith(nameof(RepublishingHandler), StringComparison.Ordinal));
        Assert.Equal(CorrelationId, handled.Scope("CorrelationId"));
        Assert.Equal(2, await host.CountAsync("OutboxMessages"));
        Assert.Equal(0, await host.ScalarAsync<long>(
            $"SELECT count(*) FROM example.\"OutboxMessages\" WHERE \"CorrelationId\" IS DISTINCT FROM '{CorrelationId}'"));
        Assert.Equal(itemId, await host.ScalarAsync<Guid>(
            "SELECT (\"Payload\"->>'itemId')::uuid FROM example.\"OutboxMessages\" WHERE \"ProcessedAt\" IS NULL"));
    }

    // A composition with the module only (no host registers ICorrelationContext) still resolves the outbox.
    [Fact]
    public async Task The_module_works_without_a_correlation_context_registration()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString })
            .Build();
        var services = new ServiceCollection().AddLogging().AddExampleModule(configuration);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await ExampleModule.MigrateDatabaseAsync(provider, TestContext.Current.CancellationToken);

        await using (var scope = provider.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<CreateExampleItemHandler>()
                .HandleAsync(new CreateExampleItemCommand("item"), TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
        }

        await using var host = ExampleTestHost.Create(connectionString, new ManualTimeProvider());
        Assert.Equal(DBNull.Value, await host.ScalarAsync<object>("SELECT \"CorrelationId\" FROM example.\"OutboxMessages\""));
    }

    // Test-only: logs, then publishes a second event through the module's outbox in the handler's scope.
    public sealed partial class RepublishingHandler(
        ILogger<RepublishingHandler> logger,
        [FromKeyedServices(ExampleApplicationRegistration.ServiceKey)] IOutbox outbox,
        [FromKeyedServices(ExampleApplicationRegistration.ServiceKey)] IUnitOfWork unitOfWork) : IIntegrationEventHandler<ExampleItemCreatedV1>
    {
        public async Task HandleAsync(ExampleItemCreatedV1 integrationEvent, CancellationToken cancellationToken)
        {
            LogHandled(logger);
            outbox.Add(integrationEvent with { });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Republishing handler ran")]
        private static partial void LogHandled(ILogger logger);
    }
}
