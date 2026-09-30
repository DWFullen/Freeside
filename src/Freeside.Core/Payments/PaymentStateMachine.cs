using System.Collections.Frozen;

namespace Freeside.Core.Payments;

/// <summary>
/// The outcome of applying a re-fetched rail invoice to a payment. <see cref="Target"/> is always
/// the state the rail reports: the processor is authoritative for payment state (AGENTS.md §2,
/// invariant 2). A <see cref="ReviewReason"/> means the payment is frozen for manual review
/// (invariant 15), whether or not the state changed.
/// </summary>
public sealed record PaymentDecision(PaymentState Current, PaymentState Target, bool OverPaid, string? ReviewReason)
{
    public bool Changed => Target != Current;

    public bool NeedsReview => ReviewReason is not null;
}

/// <summary>
/// Maps a rail's invoice to a payment state (AGENTS.md §4.4) and decides whether the move needs
/// review. A pure function that never throws: anything it can't map, or any move it doesn't expect
/// (a backward move such as <c>Paid</c> → <c>Invalid</c> after a reorg), goes to review instead
/// (invariant 15).
/// </summary>
public static class PaymentStateMachine
{
    public const PaymentState Initial = PaymentState.AwaitingPayment;

    // Moves that need no review. Anything else that changes the state is applied, and reviewed.
    private static readonly FrozenDictionary<PaymentState, FrozenSet<PaymentState>> _expected =
        new Dictionary<PaymentState, FrozenSet<PaymentState>>
        {
            [PaymentState.AwaitingPayment] = Set(
                PaymentState.PaymentSeen, PaymentState.Confirming, PaymentState.Paid, PaymentState.PaidLate,
                PaymentState.ExpiredUnderpaid, PaymentState.Expired, PaymentState.Invalid, PaymentState.Cancelled),
            [PaymentState.PaymentSeen] = Set(
                PaymentState.Confirming, PaymentState.Paid, PaymentState.PaidLate, PaymentState.ExpiredUnderpaid, PaymentState.Invalid),
            [PaymentState.Confirming] = Set(PaymentState.Paid, PaymentState.PaidLate, PaymentState.Invalid),

            // A payment that arrives after expiry (AGENTS.md §4.4: PaidLate).
            [PaymentState.Expired] = Set(PaymentState.ExpiredUnderpaid, PaymentState.Confirming, PaymentState.PaidLate, PaymentState.Invalid),
            [PaymentState.ExpiredUnderpaid] = Set(PaymentState.Confirming, PaymentState.PaidLate, PaymentState.Invalid),

            // Paid, PaidLate, Invalid and Cancelled are final: any move out of them is reviewed.
        }.ToFrozenDictionary();

    public static PaymentDecision Decide(PaymentState current, RailInvoiceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var overPaid = snapshot.Has(RailInvoiceConditions.OverPaid);
        if (!Enum.IsDefined(current) || Map(current, snapshot) is not var (target, reason))
        {
            return new PaymentDecision(current, current, overPaid, ReviewReasons.Unmapped);
        }

        if (target != current && reason is null && !IsExpected(current, target))
        {
            reason = ReviewReasons.UnexpectedTransition;
        }

        return new PaymentDecision(current, target, overPaid, reason);
    }

    public static bool IsExpected(PaymentState from, PaymentState to) =>
        _expected.TryGetValue(from, out var allowed) && allowed.Contains(to);

    private static (PaymentState Target, string? ReviewReason)? Map(PaymentState current, RailInvoiceSnapshot snapshot) =>
        snapshot.State switch
        {
            // BTCPay keeps a partly paid invoice New, with PaidPartial.
            RailInvoiceState.New => snapshot.Has(RailInvoiceConditions.PaidPartial)
                ? (PaymentState.PaymentSeen, null)
                : (PaymentState.AwaitingPayment, null),
            RailInvoiceState.PaymentSeen => (PaymentState.PaymentSeen, null),
            RailInvoiceState.Processing => (PaymentState.Confirming, null),
            RailInvoiceState.Settled => Settled(snapshot),
            RailInvoiceState.Expired => snapshot.Has(RailInvoiceConditions.PaidPartial) || snapshot.Has(RailInvoiceConditions.PaidLate)
                ? (PaymentState.ExpiredUnderpaid, null)
                : (PaymentState.Expired, null),
            RailInvoiceState.Invalid => snapshot.Has(RailInvoiceConditions.CancelledByUs) && current == PaymentState.AwaitingPayment
                ? (PaymentState.Cancelled, null)
                : (PaymentState.Invalid, ReviewReasons.Invalid),
            _ => null,
        };

    private static (PaymentState, string?) Settled(RailInvoiceSnapshot snapshot)
    {
        var target = snapshot.Has(RailInvoiceConditions.PaidLate) ? PaymentState.PaidLate : PaymentState.Paid;

        // BTCPay can't tell us who marked it or why (AGENTS.md §4.4: manuallyMarked).
        return snapshot.Has(RailInvoiceConditions.ManuallyMarked) ? (target, ReviewReasons.ManuallyMarked)
            : snapshot.Has(RailInvoiceConditions.SettlementPolicyViolated) ? (target, ReviewReasons.SettlementPolicy)
            : (target, null);
    }

    private static FrozenSet<PaymentState> Set(params PaymentState[] states) => states.ToFrozenSet();
}
