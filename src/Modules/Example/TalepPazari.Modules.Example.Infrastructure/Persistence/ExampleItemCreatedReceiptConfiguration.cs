using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TalepPazari.Modules.Example.Domain.ExampleItems;

namespace TalepPazari.Modules.Example.Infrastructure.Persistence;

internal sealed class ExampleItemCreatedReceiptConfiguration : IEntityTypeConfiguration<ExampleItemCreatedReceipt>
{
    public void Configure(EntityTypeBuilder<ExampleItemCreatedReceipt> builder)
    {
        // ItemId is deliberately not unique: a duplicate delivery must stay visible, not be hidden by a constraint.
        builder.ToTable("ExampleItemCreatedReceipts");

        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.Property(receipt => receipt.ItemId).IsRequired();
        builder.Property(receipt => receipt.ReceivedAt).IsRequired();
    }
}
