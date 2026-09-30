using Freeside.Core.Fakes;
using Freeside.Core.Monetary;

namespace Freeside.Core.Payments.Fakes;

/// <summary>
/// PLACEHOLDER(strike-api): stands in for the Strike rail (project.md §4.2, D8) until there is a
/// Strike account. Keeps Lightning invoices in memory; they settle, expire or fail only when a test
/// says so. Its destinations can't be paid. Regtest only.
/// </summary>
public sealed class FakeStrikeRail(TimeProvider time) : IPaymentRail, IFakeService
{
    /// <summary>How long a fake invoice accepts payment.</summary>
    public static readonly TimeSpan InvoiceLifetime = TimeSpan.FromMinutes(15);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _invoices = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rejectedAccounts = new(StringComparer.Ordinal);
    private bool _unavailable;

    public RailId Id => RailId.Strike;

    public RailLayer Layer => RailLayer.Lightning;

    public Task<RailInvoice> CreateAsync(PaymentRequestSpec request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_lock)
        {
            if (_unavailable)
            {
                throw new RailUnavailableException(Id, RailFailureKind.Transient, "The fake Strike rail is set to be unavailable.");
            }

            if (_rejectedAccounts.Contains(request.Account))
            {
                throw new RailAccountRejectedException(Id, request.Account, "The fake Strike rail rejects this account.");
            }

            var id = Guid.NewGuid().ToString("N");
            var invoice = new RailInvoice(
                new RailInvoiceRef(Id, request.Account, id),
                $"lnbcrt-fake-{id}",
                request.Amount,
                time.GetUtcNow() + InvoiceLifetime);
            _invoices[id] = new Entry(invoice, RailInvoiceState.New);
            return Task.FromResult(invoice);
        }
    }

    public Task<RailInvoiceSnapshot> GetAsync(RailInvoiceRef invoice, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var entry = Find(invoice);
            var state = entry.State == RailInvoiceState.New && time.GetUtcNow() >= entry.Invoice.ExpiresAt
                ? RailInvoiceState.Expired
                : entry.State;
            return Task.FromResult(new RailInvoiceSnapshot(entry.Invoice.Ref, state, RailInvoiceConditions.None));
        }
    }

    public Task CancelAsync(RailInvoiceRef invoice, CancellationToken cancellationToken = default)
    {
        Set(invoice, RailInvoiceState.Invalid, onlyFrom: RailInvoiceState.New);
        return Task.CompletedTask;
    }

    /// <summary>The buyer paid.</summary>
    public void Settle(RailInvoiceRef invoice) => Set(invoice, RailInvoiceState.Settled, onlyFrom: RailInvoiceState.New);

    /// <summary>Makes Strike refuse new invoices for <paramref name="account"/>, as for <c>canReceive: false</c>.</summary>
    public void RejectAccount(string account)
    {
        lock (_lock)
        {
            _rejectedAccounts.Add(account);
        }
    }

    /// <summary>Makes every call to create an invoice fail, as when Strike is down.</summary>
    public void SetUnavailable(bool unavailable)
    {
        lock (_lock)
        {
            _unavailable = unavailable;
        }
    }

    private void Set(RailInvoiceRef invoice, RailInvoiceState state, RailInvoiceState onlyFrom)
    {
        lock (_lock)
        {
            var entry = Find(invoice);
            if (entry.State == onlyFrom)
            {
                _invoices[invoice.ExternalId] = entry with { State = state };
            }
        }
    }

    private Entry Find(RailInvoiceRef invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        return invoice.Rail == Id && _invoices.TryGetValue(invoice.ExternalId, out var entry)
            ? entry
            : throw new InvalidOperationException($"No fake Strike invoice {invoice.ExternalId}.");
    }

    private sealed record Entry(RailInvoice Invoice, RailInvoiceState State);
}
