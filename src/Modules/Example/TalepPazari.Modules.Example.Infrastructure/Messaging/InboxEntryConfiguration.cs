using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TalepPazari.Modules.Example.Infrastructure.Messaging;

internal sealed class InboxEntryConfiguration : IEntityTypeConfiguration<InboxEntry>
{
    public const string TableName = "InboxMessages";

    public void Configure(EntityTypeBuilder<InboxEntry> builder)
    {
        builder.ToTable(TableName);

        // The key is the idempotency guarantee: one row per message and handler.
        builder.HasKey(entry => new { entry.MessageId, entry.HandlerName });
        builder.Property(entry => entry.HandlerName).HasMaxLength(InboxEntry.HandlerNameMaxLength);
        builder.Property(entry => entry.ProcessedAt).IsRequired();
    }
}
