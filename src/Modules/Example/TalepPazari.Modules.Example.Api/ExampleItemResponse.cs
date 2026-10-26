namespace TalepPazari.Modules.Example.Api;

// HTTP response body. Kept separate from Application types so the HTTP contract can change independently.
internal sealed record ExampleItemResponse(Guid Id, string Name, DateTimeOffset CreatedAt);
