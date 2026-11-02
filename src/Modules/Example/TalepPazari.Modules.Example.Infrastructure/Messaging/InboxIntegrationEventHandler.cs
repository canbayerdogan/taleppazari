using Microsoft.EntityFrameworkCore;
using TalepPazari.Modules.Example.Infrastructure.Persistence;
using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.Modules.Example.Infrastructure.Messaging;

// Wraps every Example handler. The inbox row and the handler's effect commit in one transaction,
// so a redelivered message is skipped once it has been handled. If two deliveries of one message
// overlap, the second insert waits on the primary key until the first commits, then finds the row.
internal sealed class InboxIntegrationEventHandler<TEvent>(
    IIntegrationEventHandler<TEvent> inner,
    ExampleDbContext dbContext,
    IIntegrationEventContext context,
    TimeProvider timeProvider) : IIntegrationEventHandler<TEvent>
    where TEvent : class
{
    // Full type name including namespace. Renaming a handler class makes it look new to the inbox.
    private readonly string handlerName = inner.GetType().FullName!;

    public async Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var inserted = await dbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO example."InboxMessages" ("MessageId", "HandlerName", "ProcessedAt")
            VALUES ({context.MessageId}, {handlerName}, {timeProvider.GetUtcNow()})
            ON CONFLICT DO NOTHING
            """, cancellationToken);
        if (inserted == 0)
        {
            return; // Already handled; disposing the transaction rolls back nothing of value.
        }

        try
        {
            await inner.HandleAsync(integrationEvent, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // The transaction rolls back on dispose; drop tracked entities so nothing half-saved lingers.
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
