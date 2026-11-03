using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TalepPazari.Modules.Example.Application;
using TalepPazari.Modules.Example.Application.ExampleItems;
using TalepPazari.Modules.Example.Application.ExampleItems.ListExampleItems;
using TalepPazari.Modules.Example.Application.ExampleItems.RecordExampleItemCreatedReceipt;
using TalepPazari.Modules.Example.Contracts.IntegrationEvents;
using TalepPazari.Modules.Example.Infrastructure.Messaging;
using TalepPazari.Modules.Example.Infrastructure.Persistence;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.Modules.Example.Infrastructure;

public static class ExampleModule
{
    public static IServiceCollection AddExampleModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddExampleApplication();
        services.TryAddSingleton(TimeProvider.System);

        // The options callback runs when a DbContext is first created, not at startup,
        // so a host starts even when the connection string is missing or the database is down.
        services.AddDbContext<ExampleDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException(
                    "Connection string 'ConnectionStrings:Default' is not configured. "
                    + "See docs/module-template/how-to-add-a-module.md.");

            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, ExampleDbContext.Schema));
        });

        services.AddScoped<IExampleItemRepository, ExampleItemRepository>();
        services.AddScoped<IExampleItemQueries, ExampleItemQueries>();
        services.AddScoped<IExampleItemCreatedReceiptRepository, ExampleItemCreatedReceiptRepository>();

        // Unit of work and outbox: keyed, because every module implements the same SharedKernel interfaces.
        services.AddKeyedScoped<IUnitOfWork, ExampleUnitOfWork>(ExampleApplicationRegistration.ServiceKey);
        services.AddKeyedScoped<IOutbox, ExampleOutbox>(ExampleApplicationRegistration.ServiceKey);

        // Dispatcher side (Worker): published event types and this module's outbox table.
        services.AddSingleton<ExampleIntegrationEventTypes>();
        services.AddSingleton<IIntegrationEventTypes>(provider => provider.GetRequiredService<ExampleIntegrationEventTypes>());
        services.AddScoped<IOutboxStore, ExampleOutboxStore>();

        // Consumers: every handler is exposed only through the inbox decorator.
        // IIntegrationEventContext is registered by the dispatcher (Worker host).
        AddInboxHandler<ExampleItemCreatedV1, RecordExampleItemCreatedReceiptHandler>(services);

        return services;
    }

    /// <summary>
    /// Applies this module's pending migrations. For integration tests and tooling only:
    /// hosts never call it, and migrations are never applied at startup.
    /// </summary>
    public static async Task MigrateDatabaseAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private static void AddInboxHandler<TEvent, THandler>(IServiceCollection services)
        where TEvent : class
        where THandler : IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>>(provider => new InboxIntegrationEventHandler<TEvent>(
            provider.GetRequiredService<THandler>(),
            provider.GetRequiredService<ExampleDbContext>(),
            provider.GetRequiredService<IIntegrationEventContext>(),
            provider.GetRequiredService<TimeProvider>()));
    }
}
