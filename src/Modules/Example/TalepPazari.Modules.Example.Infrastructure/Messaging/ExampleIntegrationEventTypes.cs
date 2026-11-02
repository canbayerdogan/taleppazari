using TalepPazari.Modules.Example.Contracts.IntegrationEvents;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.Modules.Example.Infrastructure.Messaging;

// Every event this module publishes. ExampleOutbox refuses any other type, and the dispatcher
// dead-letters stored type names that no registry knows.
internal sealed class ExampleIntegrationEventTypes : IIntegrationEventTypes
{
    public IReadOnlyCollection<Type> Types { get; } = [typeof(ExampleItemCreatedV1)];
}
