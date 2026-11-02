using Microsoft.Extensions.DependencyInjection;
using TalepPazari.Modules.Example.Contracts.IntegrationEvents;
using TalepPazari.Modules.Example.Domain.ExampleItems;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.Modules.Example.Application.ExampleItems.RecordExampleItemCreatedReceipt;

// The second handler from the T-007 brief: consumes ExampleItemCreatedV1 and writes a demo receipt.
// Idempotency comes from the inbox decorator the Infrastructure registration wraps around it.
public sealed class RecordExampleItemCreatedReceiptHandler(
    IExampleItemCreatedReceiptRepository repository,
    [FromKeyedServices(ExampleApplicationRegistration.ServiceKey)] IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IIntegrationEventHandler<ExampleItemCreatedV1>
{
    public async Task HandleAsync(ExampleItemCreatedV1 integrationEvent, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        repository.Add(ExampleItemCreatedReceipt.Create(Guid.CreateVersion7(now), integrationEvent.ItemId, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
