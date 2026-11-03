namespace TalepPazari.Modules.Example.Application.ExampleItems.ListExampleItems;

public sealed record ListedExampleItem(Guid Id, string Name, DateTimeOffset CreatedAt);

public interface IExampleItemQueries
{
    // Items with an Id greater than afterId (all items when null), ascending by Id, at most take items.
    Task<IReadOnlyList<ListedExampleItem>> ListAfterAsync(Guid? afterId, int take, CancellationToken cancellationToken);
}
