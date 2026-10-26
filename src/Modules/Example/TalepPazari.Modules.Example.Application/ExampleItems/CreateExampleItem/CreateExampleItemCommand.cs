namespace TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;

// Name is nullable because it arrives from the client; the validator rejects null.
public sealed record CreateExampleItemCommand(string? Name);
