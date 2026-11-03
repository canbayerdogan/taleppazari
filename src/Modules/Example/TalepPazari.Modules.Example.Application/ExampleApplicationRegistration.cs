using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;
using TalepPazari.Modules.Example.Application.ExampleItems.ListExampleItems;
using TalepPazari.Modules.Example.Application.ExampleItems.RecordExampleItemCreatedReceipt;

namespace TalepPazari.Modules.Example.Application;

public static class ExampleApplicationRegistration
{
    // Key of this module's IUnitOfWork and IOutbox. SharedKernel interfaces are shared by every module,
    // so each module registers its implementations under its own key.
    public const string ServiceKey = "Example";

    // Called by the module's Infrastructure registration; hosts call AddExampleModule instead.
    public static IServiceCollection AddExampleApplication(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CreateExampleItemCommand>, CreateExampleItemValidator>();
        services.AddScoped<CreateExampleItemHandler>();
        services.AddSingleton<IValidator<ListExampleItemsQuery>, ListExampleItemsValidator>();
        services.AddScoped<ListExampleItemsHandler>();

        // Registered as the concrete type only. Infrastructure exposes it as
        // IIntegrationEventHandler<ExampleItemCreatedV1> behind the inbox decorator.
        services.AddScoped<RecordExampleItemCreatedReceiptHandler>();
        return services;
    }
}
