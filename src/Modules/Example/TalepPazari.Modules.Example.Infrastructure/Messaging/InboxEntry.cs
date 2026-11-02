namespace TalepPazari.Modules.Example.Infrastructure.Messaging;

// One row of example."InboxMessages": the handler HandlerName has processed message MessageId.
// Written only by InboxIntegrationEventHandler, in the same transaction as the handler's effect.
internal sealed class InboxEntry
{
    public const int HandlerNameMaxLength = 500;

    public Guid MessageId { get; init; }

    public required string HandlerName { get; init; }

    public DateTimeOffset ProcessedAt { get; init; }
}
