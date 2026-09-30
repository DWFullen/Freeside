using System.Globalization;

namespace Freeside.Core.Monetary;

/// <summary>
/// A Lightning-precision amount in millisatoshis, from zero to the 21 million BTC supply
/// (2.1e18 msat, which fits in a <see cref="long"/>).
/// </summary>
public readonly record struct MilliSats
{
    public const long PerSat = 1_000;

    public const long PerBitcoin = Sats.PerBitcoin * PerSat;

    /// <summary>21 million BTC.</summary>
    public const long MaxValue = Sats.MaxValue * PerSat;

    public MilliSats(long value)
    {
        if (value is < 0 or > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Must be between 0 and {MaxValue} msat.");
        }

        Value = value;
    }

    public long Value { get; }

    /// <summary>Whole satoshis, rounded by <paramref name="rule"/>.</summary>
    public Sats ToSats(RoundingRule rule) =>
        new((long)IntegerRounding.Divide(Value, PerSat, rule));

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture) + " msat";
}
