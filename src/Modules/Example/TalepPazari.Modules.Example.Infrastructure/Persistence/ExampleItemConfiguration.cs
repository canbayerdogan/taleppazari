using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Infrastructure.Persistence;

internal sealed class ExampleItemConfiguration : IEntityTypeConfiguration<ExampleItem>
{
    public void Configure(EntityTypeBuilder<ExampleItem> builder)
    {
        // Default EF Core naming (PascalCase, quoted in SQL); no naming convention is decided yet.
        builder.ToTable("ExampleItems", table =>
            table.HasCheckConstraint("CK_ExampleItems_Name_NotBlank", "btrim(\"Name\") <> ''"));

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(ExampleItem.NameMaxLength).IsRequired();
        builder.Property(item => item.CreatedAt).IsRequired();
    }
}
