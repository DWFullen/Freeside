using Freeside.Core.Payments;
using Microsoft.Extensions.Options;

namespace Freeside.Core.Tests.Payments;

internal sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

internal sealed class OptionsBox<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; set; } = value;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>A rail that only has an identity; selection never calls it.</summary>
internal sealed class StubRail(RailId id, RailLayer layer) : IPaymentRail
{
    public RailId Id => id;

    public RailLayer Layer => layer;

    public Task<RailInvoice> CreateAsync(PaymentRequestSpec request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<RailInvoiceSnapshot> GetAsync(RailInvoiceRef invoice, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task CancelAsync(RailInvoiceRef invoice, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
