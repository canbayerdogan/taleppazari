using Microsoft.EntityFrameworkCore;
using TalepPazari.Modules.Example.Application.ExampleItems.ListExampleItems;

namespace TalepPazari.Modules.Example.Infrastructure.Persistence;

// Keyset pagination: "Id" > last id, ascending. PostgreSQL compares uuid values byte by byte, which matches
// the time order of UUID v7. Ordering happens in SQL only: .NET Guid.CompareTo uses a different byte order.
internal sealed class ExampleItemQueries(ExampleDbContext dbContext) : IExampleItemQueries
{
    public async Task<IReadOnlyList<ListedExampleItem>> ListAfterAsync(Guid? afterId, int take, CancellationToken cancellationToken)
    {
        var items = dbContext.ExampleItems.AsNoTracking();
        if (afterId is { } after)
        {
            items = items.Where(item => item.Id > after);
        }

        return await items
            .OrderBy(item => item.Id)
            .Take(take)
            .Select(item => new ListedExampleItem(item.Id, item.Name, item.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
