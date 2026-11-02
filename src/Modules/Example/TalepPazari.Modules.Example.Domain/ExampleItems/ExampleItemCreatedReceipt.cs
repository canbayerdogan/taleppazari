namespace TalepPazari.Modules.Example.Domain.ExampleItems;

// Demo effect only: the visible result of consuming ExampleItemCreatedV1, so tests can count deliveries.
public sealed class ExampleItemCreatedReceipt
{
    private ExampleItemCreatedReceipt(Guid id, Guid itemId, DateTimeOffset receivedAt)
    {
        Id = id;
        ItemId = itemId;
        ReceivedAt = receivedAt;
    }

    public Guid Id { get; private set; }

    public Guid ItemId { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public static ExampleItemCreatedReceipt Create(Guid id, Guid itemId, DateTimeOffset receivedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must not be empty.", nameof(id));
        }

        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("ItemId must not be empty.", nameof(itemId));
        }

        if (receivedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("ReceivedAt must be in UTC.", nameof(receivedAt));
        }

        return new ExampleItemCreatedReceipt(id, itemId, receivedAt);
    }
}
