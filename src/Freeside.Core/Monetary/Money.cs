using System.Globalization;
using System.Numerics;

namespace Freeside.Core.Monetary;

/// <summary>
/// A fiat amount in whole minor units (cents for USD) with its currency (AGENTS.md §2, invariant 5).
/// </summary>
public sealed record Money
{
    public Money(long minorUnits, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        MinorUnits = minorUnits;
        Currency = currency;
    }

    public long MinorUnits { get; }

    public Currency Currency { get; }

    /// <summary>Checked addition. Both amounts must be in the same currency.</summary>
    public Money Add(Money other)
    {
        RequireSameCurrency(other);
        return new Money(checked(MinorUnits + other.MinorUnits), Currency);
    }

    /// <summary>Checked subtraction. Both amounts must be in the same currency.</summary>
    public Money Subtract(Money other)
    {
        RequireSameCurrency(other);
        return new Money(checked(MinorUnits - other.MinorUnits), Currency);
    }

    /// <summary>Major units with the currency's decimal places, for example "12.34 USD" or "-0.05 USD".</summary>
    public override string ToString()
    {
        var digits = BigInteger.Abs(MinorUnits).ToString(CultureInfo.InvariantCulture);
        var exponent = Currency.MinorUnitExponent;
        if (exponent > 0)
        {
            digits = digits.PadLeft(exponent + 1, '0');
            digits = digits[..^exponent] + "." + digits[^exponent..];
        }

        return (MinorUnits < 0 ? "-" : "") + digits + " " + Currency.Code;
    }

    private void RequireSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Currency != Currency)
        {
            throw new InvalidOperationException($"Can't combine {Currency.Code} and {other.Currency.Code} amounts.");
        }
    }
}
