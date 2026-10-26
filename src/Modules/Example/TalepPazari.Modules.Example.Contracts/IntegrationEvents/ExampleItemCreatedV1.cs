namespace TalepPazari.Modules.Example.Contracts.IntegrationEvents;

// Defined only. Publishing through the outbox arrives with T-007.
public sealed record ExampleItemCreatedV1(Guid ItemId, DateTimeOffset OccurredAt);
