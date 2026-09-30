using Freeside.Core.Monetary;

namespace Freeside.Core.Tests.Monetary;

public sealed class CurrencyAndMoneyTests
{
    [Theory]
    [InlineData("USD", 2)]
    [InlineData("EUR", 2)]
    [InlineData("JPY", 0)]
    [InlineData("KWD", 3)]
    public void Known_currencies_carry_their_iso_4217_exponent(string code, int exponent) =>
        Assert.Equal(exponent, Currency.FromCode(code).MinorUnitExponent);

    [Theory]
    [InlineData("usd")]
    [InlineData("XXX")]
    [InlineData("BTC")]
    [InlineData("")]
    [InlineData(" USD")]
    public void Unknown_or_miscased_codes_are_rejected(string code)
    {
        Assert.False(Currency.TryFromCode(code, out _));
        Assert.Throws<ArgumentException>(() => Currency.FromCode(code));
    }

    [Fact]
    public void A_null_code_is_not_a_currency() => Assert.False(Currency.TryFromCode(null, out _));

    [Fact]
    public void Currencies_are_equal_by_code() => Assert.Equal(Currency.Usd, Currency.FromCode("USD"));

    [Theory]
    [InlineData(1234, "USD", "12.34 USD")]
    [InlineData(-5, "USD", "-0.05 USD")]
    [InlineData(0, "USD", "0.00 USD")]
    [InlineData(1000, "JPY", "1000 JPY")]
    [InlineData(1500, "KWD", "1.500 KWD")]
    [InlineData(long.MinValue, "USD", "-92233720368547758.08 USD")]
    public void Money_prints_major_units_with_the_currency_exponent(long minor, string code, string expected) =>
        Assert.Equal(expected, new Money(minor, Currency.FromCode(code)).ToString());

    [Fact]
    public void Money_adds_and_subtracts_within_one_currency()
    {
        var five = new Money(500, Currency.Usd);
        var two = new Money(200, Currency.Usd);

        Assert.Equal(new Money(700, Currency.Usd), five.Add(two));
        Assert.Equal(new Money(300, Currency.Usd), five.Subtract(two));
    }

    [Fact]
    public void Money_in_different_currencies_does_not_combine() =>
        Assert.Throws<InvalidOperationException>(() =>
            new Money(1, Currency.Usd).Add(new Money(1, Currency.FromCode("EUR"))));

    [Fact]
    public void Money_arithmetic_overflow_throws() =>
        Assert.Throws<OverflowException>(() =>
            new Money(long.MaxValue, Currency.Usd).Add(new Money(1, Currency.Usd)));
}
