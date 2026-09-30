using Freeside.Core.Ledger;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Freeside.Infrastructure.Ledger;

internal sealed class EfLedgerWriter(FreesideDbContext db) : ILedgerWriter
{
    public async Task<LedgerEntry> AppendAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Validate();

        db.LedgerEntries.Add(entry);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: LedgerEntryConfiguration.IdempotencyKeyIndex,
        })
        {
            throw new DuplicateLedgerEntryException(
                $"A ledger entry with idempotency key '{entry.IdempotencyKey}' already exists.", ex);
        }
        finally
        {
            // Entries are never updated, so the context doesn't keep tracking them.
            db.Entry(entry).State = EntityState.Detached;
        }

        return entry;
    }
}
