using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TalepPazari.Modules.Example.Infrastructure.Messaging;

internal sealed class OutboxEntryConfiguration : IEntityTypeConfiguration<OutboxEntry>
{
    public const string TableName = "OutboxMessages";

    public void Configure(EntityTypeBuilder<OutboxEntry> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint("CK_OutboxMessages_Attempts_NotNegative", "\"Attempts\" >= 0");
            table.HasCheckConstraint(
                "CK_OutboxMessages_NotProcessedAndDeadLettered",
                "\"ProcessedAt\" IS NULL OR \"DeadLetteredAt\" IS NULL");
        });

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Type).HasMaxLength(500).IsRequired();
        builder.Property(entry => entry.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(entry => entry.OccurredAt).IsRequired();
        builder.Property(entry => entry.TraceParent).HasMaxLength(100);
        builder.Property(entry => entry.CorrelationId).HasMaxLength(100);
        builder.Property(entry => entry.Attempts).IsRequired();
        builder.Property(entry => entry.NextAttemptAt).IsRequired();
        builder.Property(entry => entry.LastError).HasMaxLength(OutboxEntry.LastErrorMaxLength);

        // Supports the claim query: pending rows (not processed, not dead-lettered) by NextAttemptAt.
        builder.HasIndex(entry => entry.NextAttemptAt)
            .HasDatabaseName("IX_OutboxMessages_Pending_NextAttemptAt")
            .HasFilter("\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL");
    }
}
