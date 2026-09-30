using System.Numerics;

namespace Freeside.Core.Monetary;

/// <summary>
/// Converts between fiat <see cref="Money"/> and <see cref="MilliSats"/> at an
/// <see cref="ExchangeRate"/>, with exact integer arithmetic and an explicit
/// <see cref="RoundingRule"/> (AGENTS.md §2, invariant 5).
/// </summary>
public static class MoneyConverter
{
    /// <summary>msat = minor × 10^11 × rateDenominator / (10^exponent × rateNumerator).</summary>
    public static MilliSats ToMilliSats(Money amount, ExchangeRate rate, RoundingRule rule)
    {
        Validate(amount, rate);
        var numerator = amount.MinorUnits * (BigInteger)MilliSats.PerBitcoin * rate.Denominator;
        var denominator = BigInteger.Pow(10, amount.Currency.MinorUnitExponent) * rate.Numerator;
        var msat = IntegerRounding.Divide(numerator, denominator, rule);
        return msat <= MilliSats.MaxValue
            ? new MilliSats((long)msat)
            : throw new OverflowException($"{amount} at {rate} is more than 21 million BTC.");
    }

    /// <summary>minor = msat × rateNumerator × 10^exponent / (10^11 × rateDenominator).</summary>
    public static Money ToMoney(MilliSats amount, ExchangeRate rate, RoundingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rate);
        var numerator = amount.Value * rate.Numerator * BigInteger.Pow(10, rate.Quote.MinorUnitExponent);
        var denominator = MilliSats.PerBitcoin * rate.Denominator;
        var minor = IntegerRounding.Divide(numerator, denominator, rule);
        return new Money(IntegerRounding.ToInt64(minor, $"{amount} at {rate} in minor units:"), rate.Quote);
    }

    private static void Validate(Money amount, ExchangeRate rate)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(rate);
        if (amount.Currency != rate.Quote)
        {
            throw new InvalidOperationException($"Can't convert {amount.Currency.Code} at a {rate.Quote.Code} rate.");
        }

        if (amount.MinorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Only non-negative amounts can be converted.");
        }
    }
}
