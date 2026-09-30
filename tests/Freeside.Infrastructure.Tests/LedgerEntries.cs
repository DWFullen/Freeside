using Freeside.Core.Ledger;
using Freeside.Core.Monetary;

namespace Freeside.Infrastructure.Tests;

internal static class LedgerEntries
{
    public static LedgerEntry Valid(string? idempotencyKey = null) => new()
    {
        OccurredAt = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero),
        EntryType = "order.state",
        SubjectType = "order",
        SubjectId = "ord_" + Guid.NewGuid().ToString("N"),
        PreviousState = "Confirming",
        NextState = "Paid",
        Amount = new MilliSats(153_846_154),
        FiatAmount = new Money(10_000, Currency.Usd),
        Source = "btcpay-webhook",
        SourceEventId = "delivery-1",
        Actor = "system",
        CorrelationId = "corr-" + Guid.NewGuid().ToString("N"),
        IdempotencyKey = idempotencyKey,
    };
}
