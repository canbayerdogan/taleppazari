using Microsoft.Extensions.DependencyInjection;
using TalepPazari.IntegrationTests.Support;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.IntegrationTests;

// No database or Docker needed: these tests only build the production registrations.
public sealed class OutboxRegistrationTests
{
    private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2";

    [Fact]
    public async Task Every_registered_integration_event_handler_resolves_through_the_inbox_decorator()
    {
        await using var host = ExampleTestHost.Create(UnreachableDatabase, new ManualTimeProvider());
        await using var scope = host.Services.CreateAsyncScope();

        var handlers = host.Services.GetServices<IIntegrationEventTypes>()
            .SelectMany(registry => registry.Types)
            .SelectMany(eventType => scope.ServiceProvider.GetServices(typeof(IIntegrationEventHandler<>).MakeGenericType(eventType)))
            .ToList();

        Assert.NotEmpty(handlers);
        Assert.All(handlers, handler =>
        {
            var type = handler!.GetType();
            Assert.True(
                type.IsGenericType && type.GetGenericTypeDefinition().Name == "InboxIntegrationEventHandler`1",
                $"{type.FullName} is registered as an integration event handler without the inbox decorator.");
        });
    }

    [Fact]
    public async Task A_dispatcher_pass_survives_an_unavailable_database()
    {
        await using var host = ExampleTestHost.Create(UnreachableDatabase, new ManualTimeProvider());

        var result = await host.RunOnceAsync();

        Assert.Equal(new(0, 1), result);
    }
}
