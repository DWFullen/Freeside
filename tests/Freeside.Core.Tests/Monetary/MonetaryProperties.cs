using System.Globalization;
using System.Numerics;
using Freeside.Core.Monetary;
using FsCheck;
using FsCheck.Xunit;

namespace Freeside.Core.Tests.Monetary;

/// <summary>
/// Property tests over the full ranges (AGENTS.md §7.2): rounding direction, conversion bounds and
/// msat/sat round trips. <see cref="DoNotSize{T}"/> draws from the whole range, not just small values.
/// </summary>
public sealed class MonetaryProperties
{
    private static readonly Currency[] _currencies = [Currency.Usd, Currency.FromCode("JPY"), Currency.FromCode("KWD")];
    private static readonly DateTimeOffset _asOf = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Property(MaxTest = 1000)]
    public bool Sats_survive_a_round_trip_through_millisats(DoNotSize<long> raw)
    {
        var sats = new Sats((raw.Item & long.MaxValue) % (Sats.MaxValue + 1));
        return Enum.GetValues<RoundingRule>().All(rule => sats.ToMilliSats().ToSats(rule) == sats);
    }

    [Property(MaxTest = 1000)]
    public bool Rounding_millisats_to_sats_brackets_the_exact_value(DoNotSize<long> raw)
    {
        var msat = new MilliSats((raw.Item & long.MaxValue) % (MilliSats.MaxValue + 1));
        var down = msat.ToSats(RoundingRule.Down).Value;
        var up = msat.ToSats(RoundingRule.Up).Value;
        var halfEven = msat.ToSats(RoundingRule.HalfEven).Value;

        return down * MilliSats.PerSat <= msat.Value
            && msat.Value <= up * MilliSats.PerSat
            && up - down <= 1
            && (halfEven == down || halfEven == up);
    }

    [Property(MaxTest = 1000)]
    public bool Fiat_to_millisats_rounds_in_the_named_direction(DoNotSize<long> rawMinor, DoNotSize<ulong> rawRate, byte rawScale, byte rawCurrency)
    {
        var currency = _currencies[rawCurrency % _currencies.Length];
        var amount = new Money((rawMinor.Item & long.MaxValue) % 10_000_000_000_000, currency);
        var (rate, numerator, denominator) = MakeRate(rawRate.Item, rawScale, currency);

        MilliSats down, up, halfEven;
        try
        {
            down = MoneyConverter.ToMilliSats(amount, rate, RoundingRule.Down);
            up = MoneyConverter.ToMilliSats(amount, rate, RoundingRule.Up);
            halfEven = MoneyConverter.ToMilliSats(amount, rate, RoundingRule.HalfEven);
        }
        catch (OverflowException)
        {
            return true; // more than 21M BTC: rejected, which is the other half of the contract
        }

        // Exact value = N / D.
        var n = amount.MinorUnits * (BigInteger)MilliSats.PerBitcoin * denominator;
        var d = BigInteger.Pow(10, currency.MinorUnitExponent) * numerator;

        return down.Value * d <= n
            && n <= up.Value * d
            && up.Value - down.Value <= 1
            && (halfEven == down || halfEven == up);
    }

    [Property(MaxTest = 1000)]
    public bool Millisats_to_fiat_rounds_in_the_named_direction(DoNotSize<long> rawMsat, DoNotSize<ulong> rawRate, byte rawScale, byte rawCurrency)
    {
        var currency = _currencies[rawCurrency % _currencies.Length];
        var amount = new MilliSats((rawMsat.Item & long.MaxValue) % (MilliSats.MaxValue + 1));
        var (rate, numerator, denominator) = MakeRate(rawRate.Item, rawScale, currency);

        Money down, up;
        try
        {
            down = MoneyConverter.ToMoney(amount, rate, RoundingRule.Down);
            up = MoneyConverter.ToMoney(amount, rate, RoundingRule.Up);
        }
        catch (OverflowException)
        {
            return true;
        }

        var n = amount.Value * numerator * BigInteger.Pow(10, currency.MinorUnitExponent);
        var d = MilliSats.PerBitcoin * denominator;

        return down.MinorUnits * d <= n
            && n <= up.MinorUnits * d
            && up.MinorUnits - down.MinorUnits <= 1;
    }

    [Property(MaxTest = 1000)]
    public bool A_round_trip_never_crosses_the_original_amount(DoNotSize<long> rawMinor, DoNotSize<ulong> rawRate, byte rawScale)
    {
        var amount = new Money((rawMinor.Item & long.MaxValue) % 10_000_000_000_000, Currency.Usd);
        var (rate, _, _) = MakeRate(rawRate.Item, rawScale, Currency.Usd);

        try
        {
            var viaDown = MoneyConverter.ToMoney(MoneyConverter.ToMilliSats(amount, rate, RoundingRule.Down), rate, RoundingRule.Down);
            var viaUp = MoneyConverter.ToMoney(MoneyConverter.ToMilliSats(amount, rate, RoundingRule.Up), rate, RoundingRule.Up);
            return viaDown.MinorUnits <= amount.MinorUnits && amount.MinorUnits <= viaUp.MinorUnits;
        }
        catch (OverflowException)
        {
            return true;
        }
    }

    // A positive rate of up to 18 significant digits with up to 8 decimal places, plus its exact fraction.
    private static (ExchangeRate Rate, BigInteger Numerator, BigInteger Denominator) MakeRate(ulong raw, byte rawScale, Currency currency)
    {
        var scale = rawScale % 9;
        var numerator = (BigInteger)(raw % 999_999_999_999_999_999UL) + 1;
        var digits = numerator.ToString(CultureInfo.InvariantCulture).PadLeft(scale + 1, '0');
        var text = scale == 0 ? digits : digits[..^scale] + "." + digits[^scale..];
        return (new ExchangeRate(text, currency, "property", _asOf), numerator, BigInteger.Pow(10, scale));
    }
}
