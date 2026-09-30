using Freeside.Core.Monetary;

namespace Freeside.Core.Tests.Monetary;

public sealed class MoneyConverterTests
{
    private static readonly DateTimeOffset _asOf = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static ExchangeRate Rate(string rate, string code = "USD") =>
        new(rate, Currency.FromCode(code), "test", _asOf);

    [Fact]
    public void Fiat_converts_to_millisats_by_the_named_rule()
    {
        // $100.00 at $65,000.00/BTC is 153,846,153.846... msat.
        var hundredDollars = new Money(10_000, Currency.Usd);
        var rate = Rate("65000.00");

        Assert.Equal(153_846_153, MoneyConverter.ToMilliSats(hundredDollars, rate, RoundingRule.Down).Value);
        Assert.Equal(153_846_154, MoneyConverter.ToMilliSats(hundredDollars, rate, RoundingRule.Up).Value);
        Assert.Equal(153_846_154, MoneyConverter.ToMilliSats(hundredDollars, rate, RoundingRule.HalfEven).Value);
    }

    [Fact]
    public void Millisats_convert_to_fiat_by_the_named_rule()
    {
        // 153,846,154 msat at $65,000/BTC is 10,000.00001 cents.
        var amount = new MilliSats(153_846_154);
        var rate = Rate("65000");

        Assert.Equal(new Money(10_000, Currency.Usd), MoneyConverter.ToMoney(amount, rate, RoundingRule.Down));
        Assert.Equal(new Money(10_001, Currency.Usd), MoneyConverter.ToMoney(amount, rate, RoundingRule.Up));
        Assert.Equal(new Money(10_000, Currency.Usd), MoneyConverter.ToMoney(amount, rate, RoundingRule.HalfEven));
    }

    [Theory]
    [InlineData("JPY", 1_000, "10000000", 10_000_000)] // ¥1,000 at ¥10M/BTC = 10,000 sat
    [InlineData("KWD", 1_500, "20000.000", 7_500_000)] // 1.500 KWD at 20,000 KWD/BTC = 7,500 sat
    [InlineData("USD", 0, "65000", 0)]
    public void Every_currency_exponent_converts_exactly(string code, long minor, string rate, long expectedMsat)
    {
        var amount = new Money(minor, Currency.FromCode(code));

        foreach (var rule in Enum.GetValues<RoundingRule>())
        {
            Assert.Equal(expectedMsat, MoneyConverter.ToMilliSats(amount, Rate(rate, code), rule).Value);
            Assert.Equal(amount, MoneyConverter.ToMoney(new MilliSats(expectedMsat), Rate(rate, code), rule));
        }
    }

    [Fact]
    public void A_rate_in_another_currency_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() =>
            MoneyConverter.ToMilliSats(new Money(100, Currency.Usd), Rate("60000", "EUR"), RoundingRule.Down));

    [Fact]
    public void Negative_amounts_are_not_converted() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MoneyConverter.ToMilliSats(new Money(-1, Currency.Usd), Rate("65000"), RoundingRule.Down));

    [Fact]
    public void More_than_21_million_btc_overflows() =>
        Assert.Throws<OverflowException>(() =>
            MoneyConverter.ToMilliSats(new Money(long.MaxValue, Currency.Usd), Rate("0.000000000000000001"), RoundingRule.Down));

    [Fact]
    public void Fiat_beyond_64_bits_overflows() =>
        Assert.Throws<OverflowException>(() =>
            MoneyConverter.ToMoney(new MilliSats(MilliSats.MaxValue), Rate("999999999999999999.999999999999999999"), RoundingRule.Down));
}
