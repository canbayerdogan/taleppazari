using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Application.ExampleItems;

public interface IExampleItemRepository
{
    // Adds and saves immediately. A unit of work that also writes the outbox arrives with T-007.
    Task AddAsync(ExampleItem item, CancellationToken cancellationToken);
}
