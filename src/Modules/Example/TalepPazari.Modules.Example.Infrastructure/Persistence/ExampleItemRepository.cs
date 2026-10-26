using TalepPazari.Modules.Example.Application.ExampleItems;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Infrastructure.Persistence;

internal sealed class ExampleItemRepository(ExampleDbContext dbContext) : IExampleItemRepository
{
    public async Task AddAsync(ExampleItem item, CancellationToken cancellationToken)
    {
        dbContext.ExampleItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
