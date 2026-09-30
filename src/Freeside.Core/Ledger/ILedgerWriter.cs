namespace Freeside.Core.Ledger;

/// <summary>
/// Appends entries to the ledger. There is deliberately no update or delete (AGENTS.md §2,
/// invariant 7).
/// </summary>
public interface ILedgerWriter
{
    /// <summary>
    /// Validates and writes <paramref name="entry"/>, then returns it with <see cref="LedgerEntry.Id"/>
    /// and <see cref="LedgerEntry.RecordedAt"/> set. Throws <see cref="DuplicateLedgerEntryException"/>
    /// if an entry with the same idempotency key already exists.
    /// </summary>
    Task<LedgerEntry> AppendAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
}
