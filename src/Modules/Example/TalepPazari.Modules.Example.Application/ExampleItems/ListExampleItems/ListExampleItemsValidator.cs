using FluentValidation;

namespace TalepPazari.Modules.Example.Application.ExampleItems.ListExampleItems;

internal sealed class ListExampleItemsValidator : AbstractValidator<ListExampleItemsQuery>
{
    public ListExampleItemsValidator()
    {
        // Out-of-range values are rejected, never clamped silently.
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, ListExampleItemsQuery.MaxLimit)
            .When(query => query.Limit is not null);

        RuleFor(query => query.Cursor)
            .Must(cursor => ExampleItemCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'Cursor' is not a valid cursor.");
    }
}
