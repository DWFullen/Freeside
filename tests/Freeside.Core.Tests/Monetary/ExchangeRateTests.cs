using Freeside.Core.Monetary;

namespace Freeside.Core.Tests.Monetary;

public sealed class ExchangeRateTests
{
    private static readonly DateTimeOffset _utc = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("65000")]
    [InlineData("65000.12")]
    [InlineData("0.5")]
    [InlineData("000123.4500")]
    [InlineData("999999999999999999.999999999999999999")]
    public void Plain_positive_decimal_strings_are_accepted_and_kept_as_given(string rate) =>
        Assert.Equal(rate, new ExchangeRate(rate, Currency.Usd, "test", _utc).Rate);

    [Theory]
    [InlineData("")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1e5")]
    [InlineData("1E5")]
    [InlineData("+1")]
    [InlineData("1,000")]
    [InlineData("1.")]
    [InlineData(".5")]
    [InlineData("1.2.3")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("１")] // fullwidth digit one
    [InlineData("1000000000000000000")] // 19 digits before the point
    [InlineData("1.0000000000000000001")] // 19 digits after it
    public void Anything_but_a_plain_decimal_is_rejected(string rate) =>
        Assert.Throws<FormatException>(() => new ExchangeRate(rate, Currency.Usd, "test", _utc));

    [Theory]
    [InlineData("-1")]
    public void A_signed_rate_is_rejected(string rate) =>
        Assert.Throws<FormatException>(() => new ExchangeRate(rate, Currency.Usd, "test", _utc));

    [Theory]
    [InlineData("0")]
    [InlineData("0.000")]
    public void A_zero_rate_is_rejected(string rate) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExchangeRate(rate, Currency.Usd, "test", _utc));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void The_source_is_required(string source) =>
        Assert.Throws<ArgumentException>(() => new ExchangeRate("65000", Currency.Usd, source, _utc));

    [Fact]
    public void The_timestamp_must_be_utc() =>
        Assert.Throws<ArgumentException>(() =>
            new ExchangeRate("65000", Currency.Usd, "test", new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.FromHours(-4))));
}
