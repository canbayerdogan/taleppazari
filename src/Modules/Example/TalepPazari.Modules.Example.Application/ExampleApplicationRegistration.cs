using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;

namespace TalepPazari.Modules.Example.Application;

public static class ExampleApplicationRegistration
{
    // Called by the module's Infrastructure registration; hosts call AddExampleModule instead.
    public static IServiceCollection AddExampleApplication(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CreateExampleItemCommand>, CreateExampleItemValidator>();
        services.AddScoped<CreateExampleItemHandler>();
        return services;
    }
}
