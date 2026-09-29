using BTCPayServer.Client.Models;
using NBitcoin;
using NBitcoin.RPC;

namespace Freeside.Regtest.Tests;

/// <summary>
/// End-to-end check that the regtest stack works: a watch-only store, on-chain
/// invoices, payment and settlement (docs/plans/phase-0.md, PR 3).
/// </summary>
[Trait("Category", "Regtest")]
public sealed class BtcpayRegtestSmokeTests(RegtestStoreFixture stack) : IClassFixture<RegtestStoreFixture>
{
    /// <summary>
    /// Addresses checked against an invoice's address. Each run uses a new
    /// store, so its invoices take the first few indexes.
    /// </summary>
    private const int _addressWindow = 20;

    private static readonly Money _price = Money.Satoshis(100_000);

    [Fact]
    public async Task Btcpay_derives_the_same_receive_addresses_as_the_store_xpub()
    {
        var preview = await stack.Btcpay.PreviewStoreOnChainPaymentMethodAddresses(
            stack.StoreId, RegtestStack.OnChainPaymentMethod, offset: 0, amount: 10, TestContext.Current.CancellationToken);

        Assert.Equal(stack.ReceiveAddresses(10), preview.Addresses.Select(a => a.Address));
    }

    [Fact]
    public async Task Invoice_address_is_a_regtest_address_from_the_store_xpub()
    {
        var (_, onChain) = await stack.CreateInvoiceAsync(_price, TestContext.Current.CancellationToken);

        Assert.StartsWith("bcrt1", onChain.Destination, StringComparison.Ordinal);
        Assert.Contains(onChain.Destination, stack.ReceiveAddresses(_addressWindow));
        Assert.Equal(_price, Money.Coins(onChain.Due));
    }

    [Fact]
    public async Task Paid_invoice_settles_after_one_confirmation_and_not_before()
    {
        var ct = TestContext.Current.CancellationToken;
        var (invoice, onChain) = await stack.CreateInvoiceAsync(_price, ct);

        // Not RBF-signalling: BTCPay never settles a replaceable payment at 0-conf
        // anyway, so only a final one shows the speed policy is what holds it back.
        await stack.Bitcoind.SendToAddressAsync(
            BitcoinAddress.Create(onChain.Destination, Network.RegTest),
            Money.Coins(onChain.Due),
            new SendToAddressParameters { Replaceable = false },
            ct);

        // Seen but unconfirmed. MediumSpeed never settles at 0-conf (AGENTS.md §4.5),
        // so the payment itself must still be Processing: an invoice passes through
        // Processing on its way to Settled even under a 0-conf policy.
        await stack.WaitForStatusAsync(invoice.Id, InvoiceStatus.Processing, ct);
        var payment = Assert.Single((await stack.GetOnChainPaymentMethodAsync(invoice.Id, ct)).Payments);
        Assert.Equal(InvoicePaymentMethodDataModel.Payment.PaymentStatus.Processing, payment.Status);

        await stack.Bitcoind.GenerateAsync(1, ct);

        await stack.WaitForStatusAsync(invoice.Id, InvoiceStatus.Settled, ct);
    }

    [Fact]
    public async Task Each_invoice_gets_a_fresh_address()
    {
        var ct = TestContext.Current.CancellationToken;

        var (_, first) = await stack.CreateInvoiceAsync(_price, ct);
        var (_, second) = await stack.CreateInvoiceAsync(_price, ct);

        Assert.NotEqual(first.Destination, second.Destination);
        Assert.Contains(second.Destination, stack.ReceiveAddresses(_addressWindow));
    }
}
