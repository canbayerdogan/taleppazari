using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TalepPazari.Modules.Example.Application;
using TalepPazari.Modules.Example.Application.ExampleItems;
using TalepPazari.Modules.Example.Infrastructure.Persistence;

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
        return services;
    }
}
