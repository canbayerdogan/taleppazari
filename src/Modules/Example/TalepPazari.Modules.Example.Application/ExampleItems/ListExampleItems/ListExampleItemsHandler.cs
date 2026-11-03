using FluentValidation;

namespace TalepPazari.Modules.Example.Application.ExampleItems.ListExampleItems;

public sealed class ListExampleItemsHandler(IValidator<ListExampleItemsQuery> validator, IExampleItemQueries queries)
{
    public async Task<ListExampleItemsResult> HandleAsync(ListExampleItemsQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ListExampleItemsResult.Invalid(validation.ToDictionary());
        }

        var limit = query.Limit ?? ListExampleItemsQuery.DefaultLimit;
        Guid? afterId = ExampleItemCursor.TryDecode(query.Cursor, out var lastId) ? lastId : null;

        // One extra row tells whether another page exists, without a count query.
        var rows = await queries.ListAfterAsync(afterId, limit + 1, cancellationToken);
        var items = rows.Take(limit).ToList();
        var nextCursor = rows.Count > limit ? ExampleItemCursor.Encode(items[^1].Id) : null;

        return ListExampleItemsResult.Success(new ExampleItemPage(items, nextCursor));
    }
}
