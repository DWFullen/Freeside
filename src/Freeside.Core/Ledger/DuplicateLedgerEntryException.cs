namespace Freeside.Core.Ledger;

/// <summary>A ledger entry with the same idempotency key has already been written.</summary>
public sealed class DuplicateLedgerEntryException : Exception
{
    public DuplicateLedgerEntryException()
    {
    }

    public DuplicateLedgerEntryException(string message)
        : base(message)
    {
    }

    public DuplicateLedgerEntryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
