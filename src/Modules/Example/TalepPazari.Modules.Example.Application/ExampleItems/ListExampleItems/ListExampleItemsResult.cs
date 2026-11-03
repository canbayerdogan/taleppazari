using System.Diagnostics.CodeAnalysis;

namespace TalepPazari.Modules.Example.Application.ExampleItems.ListExampleItems;

// NextCursor is null on the last page.
public sealed record ExampleItemPage(IReadOnlyList<ListedExampleItem> Items, string? NextCursor);

// Module-local result type, like CreateExampleItemResult.
public sealed class ListExampleItemsResult
{
    private ListExampleItemsResult(ExampleItemPage? page, IDictionary<string, string[]> validationErrors)
    {
        Page = page;
        ValidationErrors = validationErrors;
    }

    public ExampleItemPage? Page { get; }

    public IDictionary<string, string[]> ValidationErrors { get; }

    [MemberNotNullWhen(true, nameof(Page))]
    public bool IsSuccess => Page is not null;

    public static ListExampleItemsResult Success(ExampleItemPage page) =>
        new(page, new Dictionary<string, string[]>());

    public static ListExampleItemsResult Invalid(IDictionary<string, string[]> validationErrors) =>
        new(null, validationErrors);
}
