using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Application.ExampleItems.RecordExampleItemCreatedReceipt;

public interface IExampleItemCreatedReceiptRepository
{
    // Stages the receipt; the module's IUnitOfWork commits it.
    void Add(ExampleItemCreatedReceipt receipt);
}
