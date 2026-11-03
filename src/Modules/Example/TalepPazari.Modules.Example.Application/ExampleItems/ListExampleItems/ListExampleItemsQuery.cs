namespace TalepPazari.Modules.Example.Application.ExampleItems.ListExampleItems;

// Limit and Cursor are nullable because the client may omit them; the handler applies the default.
public sealed record ListExampleItemsQuery(int? Limit, string? Cursor)
{
    // Page sizes from the API convention (docs/architecture/api-conventions.md).
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
}
