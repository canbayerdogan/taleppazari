using Microsoft.Extensions.DependencyInjection.Extensions;
using TalepPazari.SharedKernel.Messaging;
using TalepPazari.SharedKernel.Observability;

namespace TalepPazari.Worker.Outbox;

public static class OutboxDispatcherRegistration
{
    // Registers the dispatcher only. The Worker host adds the BackgroundService that runs it;
    // integration tests call OutboxDispatcher.RunOnceAsync directly.
    public static IServiceCollection AddOutboxDispatcher(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<OutboxDispatcherOptions>()
            .Bind(configuration.GetSection(OutboxDispatcherOptions.SectionName))
            .Validate(o => o.PollInterval > TimeSpan.Zero, "Outbox:PollInterval must be positive.")
            .Validate(o => o.BatchSize > 0, "Outbox:BatchSize must be positive.")
            .Validate(o => o.LeaseDuration > TimeSpan.Zero, "Outbox:LeaseDuration must be positive.")
            .Validate(o => o.MaxAttempts > 0, "Outbox:MaxAttempts must be positive.")
            .Validate(o => o.RetryBaseDelay > TimeSpan.Zero, "Outbox:RetryBaseDelay must be positive.")
            .Validate(o => o.RetryMaxDelay >= o.RetryBaseDelay, "Outbox:RetryMaxDelay must not be below RetryBaseDelay.")
            .Validate(o => o.ErrorBackoffMax >= o.PollInterval, "Outbox:ErrorBackoffMax must not be below PollInterval.")
            .Validate(o => o.AcknowledgeTimeout > TimeSpan.Zero, "Outbox:AcknowledgeTimeout must be positive.")
            .ValidateOnStart();

        services.AddScoped<IntegrationEventContext>();
        services.AddScoped<IIntegrationEventContext>(provider => provider.GetRequiredService<IntegrationEventContext>());
        services.AddScoped<ICorrelationContext>(provider => provider.GetRequiredService<IntegrationEventContext>());
        services.AddSingleton<OutboxDispatcher>();
        return services;
    }
}
