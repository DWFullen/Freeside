using System.Diagnostics;
using System.Net;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;

namespace Freeside.Regtest.Tests;

/// <summary>
/// A BTCPay store on the regtest stack, backed by a throwaway watch-only BIP84
/// account xpub. BTCPay only ever gets the xpub: the private key is dropped as
/// soon as the xpub is derived, and the store has no hot wallet (AGENTS.md §2,
/// invariant 1). Throws, so every test fails, when the stack isn't running or
/// isn't on regtest.
/// </summary>
public sealed class RegtestStoreFixture : IAsyncLifetime
{
    /// <summary>How long a payment may take to show up in BTCPay.</summary>
    private static readonly TimeSpan _statusTimeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public RPCClient Bitcoind { get; } = new(RegtestStack.BitcoindRpcCredentials, RegtestStack.BitcoindRpcUrl, Network.RegTest);

    public BTCPayServerClient Btcpay { get; private set; } = null!;

    public string StoreId { get; private set; } = null!;

    public ExtPubKey AccountXpub { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        await EnsureStackIsOnRegtestAsync(ct);

        // 1. The first user becomes the server admin. On a stack reused from an
        //    earlier run it already exists, and BTCPay refuses anonymous sign-ups.
        var anonymous = new BTCPayServerClient(RegtestStack.BtcpayUrl, _http);
        var basicAuth = new BTCPayServerClient(RegtestStack.BtcpayUrl, RegtestStack.AdminEmail, RegtestStack.AdminPassword, _http);
        try
        {
            await anonymous.CreateUser(
                new CreateApplicationUserRequest
                {
                    Email = RegtestStack.AdminEmail,
                    Password = RegtestStack.AdminPassword,
                    IsAdministrator = true,
                    SendInvitationEmail = false,
                },
                ct);

            // BTCPay accepts basic auth only in an account's first five minutes
            // unless the user opts in. Opt this throwaway admin in, so later runs
            // on the same stack can still create their API key.
            await basicAuth.UpdateCurrentUser(new UpdateApplicationUserRequest { AllowGreenfieldBasicAuth = true }, ct);
        }
        catch (GreenfieldAPIException ex) when (ex.HttpCode == (int)HttpStatusCode.Unauthorized)
        {
        }

        // An API key with only what the smoke test uses (AGENTS.md §4.6).
        ApiKeyData apiKey;
        try
        {
            apiKey = await basicAuth.CreateAPIKey(
                new CreateApiKeyRequest
                {
                    Label = "freeside-regtest-smoke",
                    Permissions =
                    [
                        Permission.Create(Policies.CanModifyStoreSettings),
                        Permission.Create(Policies.CanCreateInvoice),
                        Permission.Create(Policies.CanViewInvoices),
                    ],
                },
                ct);
        }
        catch (GreenfieldAPIException ex) when (ex.HttpCode == (int)HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                $"Can't sign in to BTCPay as {RegtestStack.AdminEmail}: {ex.Message} Recreate the stack: make regtest-down regtest-up", ex);
        }

        Btcpay = new BTCPayServerClient(RegtestStack.BtcpayUrl, apiKey.ApiKey, _http);

        // 2. A new store per run, so address indexes start from zero.
        var store = await Btcpay.CreateStore(
            new CreateStoreRequest
            {
                Name = $"freeside-smoke-{Guid.NewGuid():N}",
                DefaultCurrency = "BTC",
                SpeedPolicy = SpeedPolicy.MediumSpeed,
            },
            ct);
        StoreId = store.Id;

        AccountXpub = CreateThrowawayAccountXpub();
        await Btcpay.UpdateStorePaymentMethod(
            StoreId,
            RegtestStack.OnChainPaymentMethod,
            new UpdatePaymentMethodRequest
            {
                Enabled = true,
                Config = new JObject { ["derivationScheme"] = AccountXpub.ToString(Network.RegTest) },
            },
            ct);
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>The first <paramref name="count"/> external (0/i) P2WPKH addresses of the store's xpub.</summary>
    public IReadOnlyList<string> ReceiveAddresses(int count)
    {
        var external = AccountXpub.Derive(0);
        return [.. Enumerable.Range(0, count).Select(i => external.Derive((uint)i).PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.RegTest).ToString())];
    }

    /// <summary>Creates an on-chain-only invoice priced in BTC, so no rate source is involved.</summary>
    public async Task<(InvoiceData Invoice, InvoicePaymentMethodDataModel OnChain)> CreateInvoiceAsync(Money amount, CancellationToken ct)
    {
        var invoice = await Btcpay.CreateInvoice(
            StoreId,
            new CreateInvoiceRequest
            {
                Amount = amount.ToDecimal(MoneyUnit.BTC),
                Currency = "BTC",
                Checkout = new InvoiceDataBase.CheckoutOptions
                {
                    PaymentMethods = [RegtestStack.OnChainPaymentMethod],
                    SpeedPolicy = SpeedPolicy.MediumSpeed,
                },
            },
            ct);
        return (invoice, await GetOnChainPaymentMethodAsync(invoice.Id, ct));
    }

    public async Task<InvoicePaymentMethodDataModel> GetOnChainPaymentMethodAsync(string invoiceId, CancellationToken ct)
    {
        var paymentMethods = await Btcpay.GetInvoicePaymentMethods(invoiceId, token: ct);
        return Assert.Single(paymentMethods, m => m.PaymentMethodId == RegtestStack.OnChainPaymentMethod);
    }

    /// <summary>
    /// Polls BTCPay until the invoice reaches <paramref name="status"/>. Fails after a
    /// minute, or at once if the invoice reaches a different final status.
    /// </summary>
    public async Task<InvoiceData> WaitForStatusAsync(string invoiceId, InvoiceStatus status, CancellationToken ct)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            var invoice = await Btcpay.GetInvoice(invoiceId, token: ct);
            if (invoice.Status == status)
            {
                return invoice;
            }

            if (invoice.Status is InvoiceStatus.Settled or InvoiceStatus.Expired or InvoiceStatus.Invalid)
            {
                Assert.Fail($"Invoice {invoiceId} is {invoice.Status}; expected {status}.");
            }

            if (elapsed.Elapsed > _statusTimeout)
            {
                Assert.Fail($"Invoice {invoiceId} is {invoice.Status} after {_statusTimeout.TotalSeconds:0}s; expected {status}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }
    }

    private async Task EnsureStackIsOnRegtestAsync(CancellationToken ct)
    {
        ApiHealthData health;
        try
        {
            health = await new BTCPayServerClient(RegtestStack.BtcpayUrl, _http).GetHealth(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"No BTCPay at {RegtestStack.BtcpayUrl}. Start the regtest stack first: make regtest-up", ex);
        }

        if (!health.Synchronized)
        {
            throw new InvalidOperationException("BTCPay isn't synchronized yet. Wait for it: tools/regtest/wait.sh");
        }

        // Fail closed before anything is paid (AGENTS.md §2, invariant 4).
        var chain = (await Bitcoind.SendCommandAsync(RPCOperations.getblockchaininfo, ct)).Result.Value<string>("chain");
        if (chain != "regtest")
        {
            throw new InvalidOperationException($"bitcoind at {RegtestStack.BitcoindRpcUrl} is on '{chain}', not regtest.");
        }
    }

    private static ExtPubKey CreateThrowawayAccountXpub()
    {
        // m/84'/1'/0': BIP84, coin type 1 (every test network), account 0.
        var master = new ExtKey();
        var account = master.Derive(KeyPath.Parse("m/84'/1'/0'"));
        var xpub = account.Neuter();

        // Only the xpub is kept. Disposing a Key clears it from memory.
        account.PrivateKey.Dispose();
        master.PrivateKey.Dispose();
        return xpub;
    }
}
