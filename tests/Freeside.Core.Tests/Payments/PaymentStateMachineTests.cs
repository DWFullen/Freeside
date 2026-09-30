using Freeside.Core.Payments;
using FsCheck;
using FsCheck.Xunit;
using static Freeside.Core.Payments.PaymentState;
using C = Freeside.Core.Payments.RailInvoiceConditions;
using R = Freeside.Core.Payments.RailInvoiceState;

namespace Freeside.Core.Tests.Payments;

/// <summary>AGENTS.md §4.4 (the mapping), §2 invariants 2, 3 and 15 (review, never throw).</summary>
public sealed class PaymentStateMachineTests
{
    private static readonly RailInvoiceRef _invoice = new(RailId.BtcpayOnchain, "store-1", "inv-1");

    public static TheoryData<PaymentState, R, C, PaymentState, string?> Mapping => new()
    {
        // AGENTS.md §4.4, row by row.
        { AwaitingPayment, R.New, C.None, AwaitingPayment, null },
        { AwaitingPayment, R.PaymentSeen, C.None, PaymentSeen, null },
        { AwaitingPayment, R.New, C.PaidPartial, PaymentSeen, null },
        { PaymentSeen, R.Processing, C.None, Confirming, null },
        { Confirming, R.Settled, C.None, Paid, null },
        { Confirming, R.Settled, C.OverPaid, Paid, null },
        { PaymentSeen, R.Expired, C.PaidPartial, ExpiredUnderpaid, null },
        { AwaitingPayment, R.Expired, C.None, Expired, null },
        { Expired, R.Settled, C.PaidLate, PaidLate, null },
        { Expired, R.Processing, C.PaidLate, Confirming, null },
        { Confirming, R.Invalid, C.None, Invalid, ReviewReasons.Invalid },
        { Confirming, R.Settled, C.ManuallyMarked, Paid, ReviewReasons.ManuallyMarked },

        // Our own cancel of an unpaid invoice isn't reviewed; anything else invalid is.
        { AwaitingPayment, R.Invalid, C.CancelledByUs | C.ManuallyMarked, Cancelled, null },
        { Confirming, R.Invalid, C.CancelledByUs | C.ManuallyMarked, Invalid, ReviewReasons.Invalid },

        // Settled under a weaker speed policy than required (AGENTS.md §4.5).
        { Confirming, R.Settled, C.SettlementPolicyViolated, Paid, ReviewReasons.SettlementPolicy },

        // Backward moves: applied, and reviewed.
        { Paid, R.Invalid, C.None, Invalid, ReviewReasons.Invalid },
        { Paid, R.New, C.None, AwaitingPayment, ReviewReasons.UnexpectedTransition },
        { Paid, R.Expired, C.None, Expired, ReviewReasons.UnexpectedTransition },
        { Confirming, R.PaymentSeen, C.None, PaymentSeen, ReviewReasons.UnexpectedTransition },
        { Confirming, R.Expired, C.None, Expired, ReviewReasons.UnexpectedTransition },
        { Invalid, R.Settled, C.None, Paid, ReviewReasons.UnexpectedTransition },
        { Cancelled, R.Processing, C.None, Confirming, ReviewReasons.UnexpectedTransition },

        // No change.
        { Paid, R.Settled, C.None, Paid, null },
        { Paid, R.Settled, C.ManuallyMarked, Paid, ReviewReasons.ManuallyMarked },
    };

    [Theory]
    [MemberData(nameof(Mapping))]
    public void Maps_the_rail_state_and_flags_moves_for_review(PaymentState current, R state, C conditions, PaymentState target, string? review)
    {
        var decision = PaymentStateMachine.Decide(current, new RailInvoiceSnapshot(_invoice, state, conditions));

        Assert.Equal((target, review), (decision.Target, decision.ReviewReason));
        Assert.Equal(target != current, decision.Changed);
    }

    [Fact]
    public void Over_payment_is_carried_on_the_decision() =>
        Assert.True(PaymentStateMachine.Decide(Confirming, new RailInvoiceSnapshot(_invoice, R.Settled, C.OverPaid)).OverPaid);

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    public void An_unknown_rail_state_keeps_the_payment_and_sends_it_to_review(int state)
    {
        var decision = PaymentStateMachine.Decide(Confirming, new RailInvoiceSnapshot(_invoice, (R)state, C.None));

        Assert.Equal((Confirming, ReviewReasons.Unmapped), (decision.Target, decision.ReviewReason));
    }

    [Fact]
    public void An_unknown_current_state_is_sent_to_review() =>
        Assert.Equal(ReviewReasons.Unmapped, PaymentStateMachine.Decide((PaymentState)42, new RailInvoiceSnapshot(_invoice, R.Settled, C.None)).ReviewReason);

    /// <summary>Every state, rail state and combination of conditions, defined or not.</summary>
    [Fact]
    public void Every_combination_is_decided_without_throwing_and_never_quietly_takes_a_payment_back()
    {
        var states = Enum.GetValues<PaymentState>().Append((PaymentState)0).Append((PaymentState)99).ToList();
        var railStates = Enum.GetValues<R>().Append((R)0).Append((R)99).ToList();
        var checkedCount = 0;

        foreach (var current in states)
        {
            foreach (var railState in railStates)
            {
                for (var conditions = 0; conditions < 64; conditions++)
                {
                    var decision = PaymentStateMachine.Decide(current, new RailInvoiceSnapshot(_invoice, railState, (C)conditions));

                    if (Enum.IsDefined(current))
                    {
                        Assert.True(Enum.IsDefined(decision.Target));
                    }

                    if (current.IsPaid() && !decision.Target.IsPaid())
                    {
                        Assert.True(decision.NeedsReview, $"{current} -> {decision.Target} wasn't sent to review.");
                    }

                    if (decision.Changed && !decision.NeedsReview)
                    {
                        Assert.True(PaymentStateMachine.IsExpected(current, decision.Target), $"{current} -> {decision.Target} isn't an expected move.");
                    }

                    checkedCount++;
                }
            }
        }

        Assert.Equal(11 * 8 * 64, checkedCount);
    }

    /// <summary>Any sequence of re-fetches, applied in turn: a paid payment never becomes unpaid without review.</summary>
    [Property(MaxTest = 500)]
    public bool Any_sequence_of_snapshots_keeps_paid_payments_under_review_when_they_go_back((byte State, byte Conditions)[] snapshots)
    {
        var state = PaymentStateMachine.Initial;
        foreach (var (rawState, rawConditions) in snapshots)
        {
            var decision = PaymentStateMachine.Decide(state, new RailInvoiceSnapshot(_invoice, (R)(rawState % 8), (C)(rawConditions & 63)));
            if (state.IsPaid() && !decision.Target.IsPaid() && !decision.NeedsReview)
            {
                return false;
            }

            if (!Enum.IsDefined(decision.Target))
            {
                return false;
            }

            state = decision.Target;
        }

        return true;
    }
}
