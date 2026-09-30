using Freeside.Core.Fees;
using Freeside.Core.Fees.Fakes;
using Freeside.Core.Monetary;
using Freeside.Core.Payments;
using Freeside.Core.Payments.Fakes;

namespace Freeside.Core.Tests.Payments;

public sealed class FakeTests
{
    private static readonly PaymentRequestSpec _request = new("handle", "order-1", new MilliSats(21_000_000), "corr-1");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly ManualClock _clock = new();

    [Fact]
    public async Task The_fake_Strike_rail_settles_only_when_told_and_its_invoices_expire()
    {
        var rail = new FakeStrikeRail(_clock);
        var paid = await rail.CreateAsync(_request, Ct);
        var unpaid = await rail.CreateAsync(_request, Ct);
        Assert.Equal((RailLayer.Lightning, RailInvoiceState.New), (rail.Layer, (await rail.GetAsync(paid.Ref, Ct)).State));
        Assert.NotEqual(paid.Destination, unpaid.Destination);

        rail.Settle(paid.Ref);
        _clock.Advance(FakeStrikeRail.InvoiceLifetime);

        Assert.Equal(RailInvoiceState.Settled, (await rail.GetAsync(paid.Ref, Ct)).State);
        Assert.Equal(RailInvoiceState.Expired, (await rail.GetAsync(unpaid.Ref, Ct)).State);
    }

    [Fact]
    public async Task Cancelling_invalidates_an_unpaid_fake_invoice_but_not_a_paid_one()
    {
        var rail = new FakeStrikeRail(_clock);
        var unpaid = await rail.CreateAsync(_request, Ct);
        var paid = await rail.CreateAsync(_request, Ct);
        rail.Settle(paid.Ref);

        await rail.CancelAsync(unpaid.Ref, Ct);
        await rail.CancelAsync(paid.Ref, Ct);

        Assert.Equal(RailInvoiceState.Invalid, (await rail.GetAsync(unpaid.Ref, Ct)).State);
        Assert.Equal(RailInvoiceState.Settled, (await rail.GetAsync(paid.Ref, Ct)).State);
    }

    [Fact]
    public async Task The_fake_Strike_rail_can_reject_an_account_or_be_down()
    {
        var rail = new FakeStrikeRail(_clock);
        rail.RejectAccount("handle");

        var rejected = await Assert.ThrowsAsync<RailAccountRejectedException>(() => rail.CreateAsync(_request, Ct));
        Assert.Equal((RailId.Strike, "handle"), (rejected.Rail, rejected.Account));

        rail.SetUnavailable(true);
        var down = await Assert.ThrowsAsync<RailUnavailableException>(() => rail.CreateAsync(_request with { }, Ct));
        Assert.Equal(RailFailureKind.Transient, down.Kind);
    }

    [Fact]
    public async Task The_fake_fee_collector_is_idempotent_and_records_returns()
    {
        var collector = new FakeFeeCollector(_clock);
        var request = new DebitRequest("dev-1", "mandate-1", new Money(12_345, Currency.Usd), "debit-2026-10");

        var id = await collector.InitiateDebitAsync(request, Ct);
        Assert.Equal(id, await collector.InitiateDebitAsync(request, Ct));
        Assert.Equal(DebitState.Pending, (await collector.GetDebitAsync(id, Ct)).State);

        collector.Return(id, "R01");
        var status = await collector.GetDebitAsync(id, Ct);
        Assert.Equal((DebitState.Returned, "R01"), (status.State, status.ReturnCode));
    }

    [Fact]
    public void Debits_are_positive_USD() =>
        Assert.Throws<ArgumentException>(() => new DebitRequest("dev-1", "mandate-1", new Money(100, Currency.FromCode("EUR")), "k"));
}
