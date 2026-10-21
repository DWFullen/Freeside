using Freeside.Core.Monetary;

namespace Freeside.Core.Fees;

/// <summary>
/// Collects the platform's own fees from a developer's bank account by ACH debit (project.md §4.4,
/// P1: the platform's revenue only, never sale proceeds). Mandates and bank linking come with the
/// fee account in Phase 1.
/// </summary>
public interface IFeeCollector
{
    /// <summary>Starts a debit. Calling again with the same idempotency key returns the same debit.</summary>
    Task<DebitId> InitiateDebitAsync(DebitRequest request, CancellationToken cancellationToken = default);

    Task<DebitStatus> GetDebitAsync(DebitId debit, CancellationToken cancellationToken = default);
}

public readonly record struct DebitId
{
    public DebitId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

/// <summary>A debit of accrued fees, in USD (project.md §4.4, D13).</summary>
public sealed record DebitRequest
{
    public DebitRequest(string developerId, string mandateRef, Money amount, string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(developerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mandateRef);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (amount.Currency != Currency.Usd)
        {
            throw new ArgumentException("Fee accounts are kept in USD (project.md D13).", nameof(amount));
        }

        if (amount.MinorUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A debit needs a positive amount.");
        }

        DeveloperId = developerId;
        MandateRef = mandateRef;
        Amount = amount;
        IdempotencyKey = idempotencyKey;
    }

    public string DeveloperId { get; }

    /// <summary>The ACH provider's token for the developer's linked account. Never raw account numbers (project.md §4.4).</summary>
    public string MandateRef { get; }

    public Money Amount { get; }

    public string IdempotencyKey { get; }
}

public enum DebitState
{
    Pending = 1,
    Settled = 2,

    /// <summary>The bank returned it; <see cref="DebitStatus.ReturnCode"/> says why (for example R01).</summary>
    Returned = 3,
}

public sealed record DebitStatus(DebitId Id, DebitState State, string? ReturnCode, DateTimeOffset UpdatedAt);
