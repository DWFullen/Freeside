using Freeside.Core.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freeside.Infrastructure.Messaging;

internal static class QueueColumns
{
    public const int MaxTypeLength = 100;
    public const int MaxDedupeKeyLength = 200;
    public const int MaxErrorLength = 2000;

    public static void Configure<T>(EntityTypeBuilder<T> entity, string table)
        where T : QueueRecord
    {
        entity.ToTable(table, t =>
        {
            t.HasCheckConstraint($"ck_{table}_status", "status IN ('Pending', 'Running', 'Succeeded', 'Dead')");
            t.HasCheckConstraint($"ck_{table}_attempts", "attempts >= 0 AND max_attempts >= 1");
        });

        entity.HasKey(r => r.Id);
        entity.Property(r => r.Id).UseIdentityAlwaysColumn();
        entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).HasDefaultValue(WorkStatus.Pending);
        entity.Property(r => r.Attempts).HasDefaultValue(0);
        entity.Property(r => r.RunAfter).HasDefaultValueSql("now()");
        entity.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(r => r.LockedBy).HasMaxLength(LedgerEntry.MaxIdLength);
        entity.Property(r => r.LastError).HasMaxLength(MaxErrorLength);
        entity.Property(r => r.CorrelationId).HasMaxLength(LedgerEntry.MaxIdLength);

        // What the claim query and the lease sweep scan.
        entity.HasIndex(r => new { r.RunAfter, r.Id }).HasFilter("status = 'Pending'").HasDatabaseName($"ix_{table}_pending");
        entity.HasIndex(r => r.LockedUntil).HasFilter("status = 'Running'").HasDatabaseName($"ix_{table}_running");
    }
}

internal sealed class JobRecordConfiguration : IEntityTypeConfiguration<JobRecord>
{
    public void Configure(EntityTypeBuilder<JobRecord> entity)
    {
        QueueColumns.Configure(entity, "jobs");
        entity.Property(j => j.JobType).HasMaxLength(QueueColumns.MaxTypeLength);
        entity.Property(j => j.Payload).HasColumnType("jsonb");
        entity.Property(j => j.DedupeKey).HasMaxLength(QueueColumns.MaxDedupeKeyLength);
        entity.HasIndex(j => j.DedupeKey).IsUnique().HasDatabaseName("ux_jobs_dedupe_key");
    }
}

internal sealed class InboxRecordConfiguration : IEntityTypeConfiguration<InboxRecord>
{
    public void Configure(EntityTypeBuilder<InboxRecord> entity)
    {
        QueueColumns.Configure(entity, "inbox_messages");
        entity.ToTable(t => t.HasCheckConstraint("ck_inbox_messages_payload_length", $"length(payload) <= {EfInbox.MaxPayloadLength}"));
        entity.Property(m => m.Source).HasMaxLength(LedgerEntry.MaxNameLength);
        entity.Property(m => m.DedupeKey).HasMaxLength(QueueColumns.MaxDedupeKeyLength);
        entity.Property(m => m.MessageType).HasMaxLength(QueueColumns.MaxTypeLength);

        // The verified raw body, kept exactly as received (not normalized as jsonb).
        entity.Property(m => m.Payload).HasColumnType("text");
        entity.HasIndex(m => new { m.Source, m.DedupeKey }).IsUnique().HasDatabaseName("ux_inbox_messages_source_dedupe_key");
    }
}

internal sealed class OutboxRecordConfiguration : IEntityTypeConfiguration<OutboxRecord>
{
    public void Configure(EntityTypeBuilder<OutboxRecord> entity)
    {
        QueueColumns.Configure(entity, "outbox_messages");
        entity.Property(m => m.MessageType).HasMaxLength(QueueColumns.MaxTypeLength);
        entity.Property(m => m.Payload).HasColumnType("jsonb");
    }
}
