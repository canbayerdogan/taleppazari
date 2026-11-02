using Microsoft.EntityFrameworkCore;
using TalepPazari.Modules.Example.Domain.ExampleItems;
using TalepPazari.Modules.Example.Infrastructure.Messaging;

namespace TalepPazari.Modules.Example.Infrastructure.Persistence;

internal sealed class ExampleDbContext(DbContextOptions<ExampleDbContext> options) : DbContext(options)
{
    internal const string Schema = "example";

    public DbSet<ExampleItem> ExampleItems => Set<ExampleItem>();

    public DbSet<ExampleItemCreatedReceipt> ExampleItemCreatedReceipts => Set<ExampleItemCreatedReceipt>();

    public DbSet<OutboxEntry> OutboxMessages => Set<OutboxEntry>();

    public DbSet<InboxEntry> InboxMessages => Set<InboxEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new ExampleItemConfiguration());
        modelBuilder.ApplyConfiguration(new ExampleItemCreatedReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxEntryConfiguration());
        modelBuilder.ApplyConfiguration(new InboxEntryConfiguration());
    }
}
