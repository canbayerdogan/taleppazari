namespace TalepPazari.SharedKernel.Messaging;

// Commits a module's pending state changes and outbox messages in one transaction.
// Each module registers its own implementation as a keyed service (key: the module name).
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
