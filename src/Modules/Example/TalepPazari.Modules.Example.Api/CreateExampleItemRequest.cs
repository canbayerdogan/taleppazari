namespace TalepPazari.Modules.Example.Api;

// HTTP request body. Name is nullable because the client may omit it; the Application validator rejects null.
internal sealed record CreateExampleItemRequest(string? Name);
