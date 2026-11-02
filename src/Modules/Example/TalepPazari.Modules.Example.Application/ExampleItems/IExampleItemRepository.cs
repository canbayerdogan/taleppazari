using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Application.ExampleItems;

public interface IExampleItemRepository
{
    // Stages the item; the module's IUnitOfWork commits it together with any outbox message.
    void Add(ExampleItem item);
}
