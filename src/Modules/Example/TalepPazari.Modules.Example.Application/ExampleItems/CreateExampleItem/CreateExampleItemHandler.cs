using FluentValidation;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;

public sealed class CreateExampleItemHandler(
    IValidator<CreateExampleItemCommand> validator,
    IExampleItemRepository repository,
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
        await repository.AddAsync(item, cancellationToken);

        return CreateExampleItemResult.Success(new CreatedExampleItem(item.Id, item.Name, item.CreatedAt));
    }
}
