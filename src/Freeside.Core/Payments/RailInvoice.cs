using Freeside.Core.Monetary;

namespace Freeside.Core.Payments;

/// <summary>
/// Where a rail invoice lives: the account it pays into (a BTCPay store ID, a Strike handle) and
/// the rail's own invoice ID.
/// </summary>
public sealed record RailInvoiceRef
{
    public RailInvoiceRef(RailId rail, string account, string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        Rail = rail;
        Account = account;
        ExternalId = externalId;
    }

    public RailId Rail { get; }

    public string Account { get; }

    public string ExternalId { get; }
}

/// <summary>
/// What checkout asks a rail for. The amount is locked by the caller in msat (AGENTS.md §2,
/// invariant 5); on-chain rails require whole satoshis. <see cref="OrderRef"/> is opaque and never
/// holds buyer data (invariant 10).
/// </summary>
public sealed record PaymentRequestSpec
{
    public PaymentRequestSpec(string account, string orderRef, MilliSats amount, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(orderRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        if (amount.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A payment request needs a positive amount.");
        }

        Account = account;
        OrderRef = orderRef;
        Amount = amount;
        CorrelationId = correlationId;
    }

    public string Account { get; }

    public string OrderRef { get; }

    public MilliSats Amount { get; }

    public string CorrelationId { get; }
}

/// <summary>A created invoice: where the buyer pays (an address or a BOLT11 invoice), how much, and until when.</summary>
public sealed record RailInvoice(RailInvoiceRef Ref, string Destination, MilliSats Amount, DateTimeOffset ExpiresAt);

/// <summary>The rail's own view of an invoice, normalized. Payment state comes only from here (AGENTS.md §2, invariant 2).</summary>
public enum RailInvoiceState
{
    /// <summary>Created; nothing paid.</summary>
    New = 1,

    /// <summary>Some payment seen, not the full amount.</summary>
    PaymentSeen = 2,

    /// <summary>The full amount seen, not yet settled under the settlement policy.</summary>
    Processing = 3,

    Settled = 4,

    Expired = 5,

    Invalid = 6,
}

[Flags]
public enum RailInvoiceConditions
{
    None = 0,
    OverPaid = 1,
    PaidPartial = 2,
    PaidLate = 4,

    /// <summary>An operator changed the status by hand in the rail's own UI.</summary>
    ManuallyMarked = 8,

    /// <summary>We asked the rail to cancel this invoice. Set from our own records, not by the rail.</summary>
    CancelledByUs = 16,

    /// <summary>The rail settled the invoice under a weaker policy than we require (AGENTS.md §4.5).</summary>
    SettlementPolicyViolated = 32,
}

/// <summary>A re-fetched invoice (AGENTS.md §4.6: re-read before acting).</summary>
public sealed record RailInvoiceSnapshot(RailInvoiceRef Ref, RailInvoiceState State, RailInvoiceConditions Conditions)
{
    public bool Has(RailInvoiceConditions condition) => (Conditions & condition) == condition;
}

/// <summary>
/// A notification that an invoice changed, parsed from a webhook. It only triggers a re-fetch: its
/// contents are never used as payment state.
/// </summary>
public sealed record InvoiceEvent(RailInvoiceRef Invoice, string DeliveryId, string EventType, DateTimeOffset OccurredAt);
