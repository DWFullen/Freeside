using Freeside.Core.Ledger;
using Freeside.Core.Monetary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Freeside.Infrastructure.Ledger;

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public const string TableName = "ledger_entries";
    public const string IdempotencyKeyIndex = "ux_ledger_entries_idempotency_key";

    public void Configure(EntityTypeBuilder<LedgerEntry> entry)
    {
        entry.ToTable(TableName, table =>
        {
            table.HasCheckConstraint("ck_ledger_entries_amount_msat", "amount_msat >= 0");
            table.HasCheckConstraint("ck_ledger_entries_fiat_amount_minor", "fiat_amount_minor >= 0");
            table.HasCheckConstraint("ck_ledger_entries_fiat_pair", "(fiat_amount_minor IS NULL) = (fiat_currency IS NULL)");
            table.HasCheckConstraint("ck_ledger_entries_fiat_currency", "fiat_currency ~ '^[A-Z]{3}$'");
        });

        entry.HasKey(e => e.Id);
        entry.Property(e => e.Id).UseIdentityAlwaysColumn();
        entry.Property(e => e.RecordedAt).HasDefaultValueSql("now()").ValueGeneratedOnAdd();

        entry.Property(e => e.EntryType).HasMaxLength(LedgerEntry.MaxNameLength);
        entry.Property(e => e.SubjectType).HasMaxLength(LedgerEntry.MaxNameLength);
        entry.Property(e => e.SubjectId).HasMaxLength(LedgerEntry.MaxIdLength);
        entry.Property(e => e.PreviousState).HasMaxLength(LedgerEntry.MaxNameLength);
        entry.Property(e => e.NextState).HasMaxLength(LedgerEntry.MaxNameLength);
        entry.Property(e => e.Source).HasMaxLength(LedgerEntry.MaxNameLength);
        entry.Property(e => e.SourceEventId).HasMaxLength(LedgerEntry.MaxIdLength);
        entry.Property(e => e.Actor).HasMaxLength(LedgerEntry.MaxIdLength);
        entry.Property(e => e.Reason).HasMaxLength(LedgerEntry.MaxReasonLength);
        entry.Property(e => e.CorrelationId).HasMaxLength(LedgerEntry.MaxIdLength);
        entry.Property(e => e.IdempotencyKey).HasMaxLength(LedgerEntry.MaxIdempotencyKeyLength);

        // Money columns are bigint (AGENTS.md §7.5).
        entry.Property(e => e.Amount)
            .HasColumnName("amount_msat")
            .HasConversion(new ValueConverter<MilliSats, long>(amount => amount.Value, value => new MilliSats(value)));
        entry.Ignore(e => e.FiatAmount);
        entry.Property<long?>("_fiatAmountMinor").HasColumnName("fiat_amount_minor");
        entry.Property<string?>("_fiatCurrency").HasColumnName("fiat_currency").HasMaxLength(3);

        entry.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName(IdempotencyKeyIndex);
        entry.HasIndex(e => new { e.SubjectType, e.SubjectId });
    }
}
