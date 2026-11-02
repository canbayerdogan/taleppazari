using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TalepPazari.Modules.Example.Contracts.IntegrationEvents;
using TalepPazari.Modules.Example.Domain.ExampleItems;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;

public sealed class CreateExampleItemHandler(
    IValidator<CreateExampleItemCommand> validator,
    IExampleItemRepository repository,
    [FromKeyedServices(ExampleApplicationRegistration.ServiceKey)] IOutbox outbox,
    [FromKeyedServices(ExampleApplicationRegistration.ServiceKey)] IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CreateExampleItemResult> HandleAsync(
        CreateExampleItemCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return CreateExampleItemResult.Invalid(validation.ToDictionary());
        }

        var now = timeProvider.GetUtcNow();
        var item = ExampleItem.Create(Guid.CreateVersion7(now), command.Name!, now);

        // The item and its event are committed in one transaction (outbox pattern, ADR-003).
        repository.Add(item);
        outbox.Add(new ExampleItemCreatedV1(item.Id, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateExampleItemResult.Success(new CreatedExampleItem(item.Id, item.Name, item.CreatedAt));
    }
}
