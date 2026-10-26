using System.Diagnostics.CodeAnalysis;

namespace TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;

public sealed record CreatedExampleItem(Guid Id, string Name, DateTimeOffset CreatedAt);

// Module-local result type. A shared Result and error model are decided when a second module needs them.
public sealed class CreateExampleItemResult
{
    private CreateExampleItemResult(CreatedExampleItem? item, IDictionary<string, string[]> validationErrors)
    {
        Item = item;
        ValidationErrors = validationErrors;
    }

    public CreatedExampleItem? Item { get; }

    public IDictionary<string, string[]> ValidationErrors { get; }

    [MemberNotNullWhen(true, nameof(Item))]
    public bool IsSuccess => Item is not null;

    public static CreateExampleItemResult Success(CreatedExampleItem item) =>
        new(item, new Dictionary<string, string[]>());

    public static CreateExampleItemResult Invalid(IDictionary<string, string[]> validationErrors) =>
        new(null, validationErrors);
}
