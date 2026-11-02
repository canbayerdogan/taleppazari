namespace TalepPazari.SharedKernel.Messaging;

// The integration event types one module publishes. Each module registers one implementation as a
// singleton; the dispatcher resolves stored type names only through these registries, never by loading
// a type from a database string. Stored names are Type.FullName (no assembly name or version).
public interface IIntegrationEventTypes
{
    IReadOnlyCollection<Type> Types { get; }
}
