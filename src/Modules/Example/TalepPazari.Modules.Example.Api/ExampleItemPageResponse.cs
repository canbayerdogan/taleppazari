namespace TalepPazari.Modules.Example.Api;

// One page of a cursor-paginated list. NextCursor is opaque and null on the last page.
internal sealed record ExampleItemPageResponse(IReadOnlyList<ExampleItemResponse> Items, string? NextCursor);
