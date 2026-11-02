using TalepPazari.Modules.Example.Application.ExampleItems;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Infrastructure.Persistence;

internal sealed class ExampleItemRepository(ExampleDbContext dbContext) : IExampleItemRepository
{
    public void Add(ExampleItem item) => dbContext.ExampleItems.Add(item);
}
