namespace TalepPazari.Modules.Example.Contracts.IntegrationEvents;

// Published through the Example outbox when an item is created; stored under its full type name,
// so renaming or moving it breaks messages that are still pending.
public sealed record ExampleItemCreatedV1(Guid ItemId, DateTimeOffset OccurredAt);
