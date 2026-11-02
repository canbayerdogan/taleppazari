using TalepPazari.Modules.Example.Application.ExampleItems.RecordExampleItemCreatedReceipt;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Infrastructure.Persistence;

internal sealed class ExampleItemCreatedReceiptRepository(ExampleDbContext dbContext) : IExampleItemCreatedReceiptRepository
{
    public void Add(ExampleItemCreatedReceipt receipt) => dbContext.ExampleItemCreatedReceipts.Add(receipt);
}
