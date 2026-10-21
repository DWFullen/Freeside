namespace Freeside.Core.Payments;

/// <summary>
/// A way a buyer pays a developer directly (project.md §4.2, P1). Implementations report failures
/// with <see cref="RailUnavailableException"/> (the rail as a whole) or
/// <see cref="RailAccountRejectedException"/> (one developer's account), which feed
/// <see cref="RailHealth"/>.
/// </summary>
public interface IPaymentRail
{
    RailId Id { get; }

    RailLayer Layer { get; }

    Task<RailInvoice> CreateAsync(PaymentRequestSpec request, CancellationToken cancellationToken = default);

    Task<RailInvoiceSnapshot> GetAsync(RailInvoiceRef invoice, CancellationToken cancellationToken = default);

    /// <summary>Stops the invoice accepting payment, for example the other rail of a dual-rail order.</summary>
    Task CancelAsync(RailInvoiceRef invoice, CancellationToken cancellationToken = default);
}

public enum RailFailureKind
{
    /// <summary>Errors, timeouts, the rail being down.</summary>
    Transient = 1,

    /// <summary>Our credentials were refused. Opens the breaker at once.</summary>
    Authentication = 2,
}

/// <summary>The rail can't serve anyone right now.</summary>
public sealed class RailUnavailableException : Exception
{
    public RailUnavailableException()
    {
    }

    public RailUnavailableException(string message)
        : base(message)
    {
    }

    public RailUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public RailUnavailableException(RailId rail, RailFailureKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Rail = rail;
        Kind = kind;
    }

    public RailId Rail { get; }

    public RailFailureKind Kind { get; } = RailFailureKind.Transient;
}

/// <summary>The rail refuses one developer's account (for example Strike's <c>canReceive: false</c>).</summary>
public sealed class RailAccountRejectedException : Exception
{
    public RailAccountRejectedException()
    {
    }

    public RailAccountRejectedException(string message)
        : base(message)
    {
    }

    public RailAccountRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public RailAccountRejectedException(RailId rail, string account, string message)
        : base(message)
    {
        Rail = rail;
        Account = account;
    }

    public RailId Rail { get; }

    public string? Account { get; }
}
