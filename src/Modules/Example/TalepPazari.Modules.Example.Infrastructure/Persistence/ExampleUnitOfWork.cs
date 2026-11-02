using TalepPazari.SharedKernel.Messaging;

namespace TalepPazari.Modules.Example.Infrastructure.Persistence;

// One SaveChanges is one transaction, so staged state changes and outbox rows commit together.
// Inside the inbox decorator it joins the decorator's open transaction instead.
internal sealed class ExampleUnitOfWork(ExampleDbContext dbContext) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
