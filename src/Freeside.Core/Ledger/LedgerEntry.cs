using Freeside.Core.Monetary;

namespace Freeside.Core.Ledger;

/// <summary>
/// One row of the append-only ledger (AGENTS.md §2, invariant 7): a payment or entitlement state
/// change, or a fee movement, with its source event, actor and correlation ID. Entries are
/// immutable once built and are never updated or deleted; the database enforces that too.
/// Amounts are never negative: whether an entry is a credit or a debit follows from
/// <see cref="EntryType"/>, and balances are always derived from the entries.
/// </summary>
public sealed class LedgerEntry
{
    public const int MaxNameLength = 64;
    public const int MaxIdLength = 128;
    public const int MaxIdempotencyKeyLength = 200;
    public const int MaxReasonLength = 1000;

    private readonly long? _fiatAmountMinor;
    private readonly string? _fiatCurrency;

    /// <summary>Assigned by the database.</summary>
    public long Id { get; private set; }

    /// <summary>When the row was written, by the database clock.</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>When the event happened. Must be UTC.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>What kind of change this is, for example "order.state" or "fee.accrued".</summary>
    public required string EntryType { get; init; }

    /// <summary>What the entry is about, for example "order", "entitlement" or "debit".</summary>
    public required string SubjectType { get; init; }

    public required string SubjectId { get; init; }

    public string? PreviousState { get; init; }

    public string? NextState { get; init; }

    public MilliSats? Amount { get; init; }

    public Money? FiatAmount
    {
        get => _fiatAmountMinor is { } minor && _fiatCurrency is { } code ? new Money(minor, Currency.FromCode(code)) : null;
        init
        {
            _fiatAmountMinor = value?.MinorUnits;
            _fiatCurrency = value?.Currency.Code;
        }
    }

    /// <summary>Where the change came from, for example "btcpay-webhook", "reconciliation" or "operator".</summary>
    public required string Source { get; init; }

    /// <summary>The source's own event ID, for example BTCPay's <c>deliveryId</c>.</summary>
    public string? SourceEventId { get; init; }

    /// <summary>Who or what made the change: "system", or an operator's identity.</summary>
    public required string Actor { get; init; }

    /// <summary>Why, when a person made the change (manual marks need one, AGENTS.md §4.4).</summary>
    public string? Reason { get; init; }

    /// <summary>Carries through invoice, webhook, entitlement and download (AGENTS.md §7.5).</summary>
    public required string CorrelationId { get; init; }

    /// <summary>When set, a second entry with the same key is rejected.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Throws <see cref="ArgumentException"/> listing every problem with this entry.</summary>
    public void Validate()
    {
        var problems = new List<string>();

        if (OccurredAt.Offset != TimeSpan.Zero)
        {
            problems.Add($"{nameof(OccurredAt)} must be UTC.");
        }

        Required(problems, nameof(EntryType), EntryType, MaxNameLength);
        Required(problems, nameof(SubjectType), SubjectType, MaxNameLength);
        Required(problems, nameof(SubjectId), SubjectId, MaxIdLength);
        Required(problems, nameof(Source), Source, MaxNameLength);
        Required(problems, nameof(Actor), Actor, MaxIdLength);
        Required(problems, nameof(CorrelationId), CorrelationId, MaxIdLength);
        Optional(problems, nameof(PreviousState), PreviousState, MaxNameLength);
        Optional(problems, nameof(NextState), NextState, MaxNameLength);
        Optional(problems, nameof(SourceEventId), SourceEventId, MaxIdLength);
        Optional(problems, nameof(Reason), Reason, MaxReasonLength);
        Optional(problems, nameof(IdempotencyKey), IdempotencyKey, MaxIdempotencyKeyLength);

        if (_fiatAmountMinor < 0)
        {
            problems.Add($"{nameof(FiatAmount)} must not be negative.");
        }

        if (problems.Count > 0)
        {
            throw new ArgumentException("Invalid ledger entry: " + string.Join(" ", problems));
        }
    }

    private static void Required(List<string> problems, string name, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add($"{name} is required.");
            return;
        }

        Optional(problems, name, value, maxLength);
    }

    private static void Optional(List<string> problems, string name, string? value, int maxLength)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length == 0 || value.Trim().Length != value.Length)
        {
            problems.Add($"{name} must not be empty or have leading or trailing whitespace.");
        }

        if (value.Length > maxLength)
        {
            problems.Add($"{name} is longer than {maxLength} characters.");
        }
    }
}
