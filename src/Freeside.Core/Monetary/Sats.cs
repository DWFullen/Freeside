using System.Globalization;

namespace Freeside.Core.Monetary;

/// <summary>
/// An on-chain amount in satoshis, from zero to the 21 million BTC supply (AGENTS.md §2, invariant 5).
/// </summary>
public readonly record struct Sats
{
    public const long PerBitcoin = 100_000_000;

    /// <summary>21 million BTC.</summary>
    public const long MaxValue = 21_000_000 * PerBitcoin;

    public Sats(long value)
    {
        if (value is < 0 or > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Must be between 0 and {MaxValue} sat.");
        }

        Value = value;
    }

    public long Value { get; }

    /// <summary>Exact: every satoshi is 1,000 millisatoshis.</summary>
    public MilliSats ToMilliSats() => new(Value * MilliSats.PerSat);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture) + " sat";
}
