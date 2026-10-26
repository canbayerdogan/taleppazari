using Microsoft.EntityFrameworkCore;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Infrastructure.Persistence;

internal sealed class ExampleDbContext(DbContextOptions<ExampleDbContext> options) : DbContext(options)
{
    internal const string Schema = "example";

    public DbSet<ExampleItem> ExampleItems => Set<ExampleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new ExampleItemConfiguration());
    }
}
