namespace Freeside.Core.Payments;

/// <summary>An order's payment state (AGENTS.md §4.4). Stored by name.</summary>
public enum PaymentState
{
    AwaitingPayment = 1,
    PaymentSeen = 2,
    Confirming = 3,
    Paid = 4,
    PaidLate = 5,
    ExpiredUnderpaid = 6,
    Expired = 7,
    Invalid = 8,
    Cancelled = 9,
}

public static class PaymentStates
{
    /// <summary>States that grant the entitlement (AGENTS.md §2, invariant 3).</summary>
    public static bool IsPaid(this PaymentState state) => state is PaymentState.Paid or PaymentState.PaidLate;
}

/// <summary>Why a payment went to the manual-review queue (AGENTS.md §2, invariant 15).</summary>
public static class ReviewReasons
{
    public const string ManuallyMarked = "manually-marked";
    public const string Invalid = "invalid";
    public const string SettlementPolicy = "settlement-policy";
    public const string UnexpectedTransition = "unexpected-transition";
    public const string Unmapped = "unmapped";
}
