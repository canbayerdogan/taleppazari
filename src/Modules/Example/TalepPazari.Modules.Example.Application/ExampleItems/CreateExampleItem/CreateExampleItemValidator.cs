using FluentValidation;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;

public sealed class CreateExampleItemValidator : AbstractValidator<CreateExampleItemCommand>
{
    public CreateExampleItemValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(ExampleItem.NameMaxLength);
    }
}
