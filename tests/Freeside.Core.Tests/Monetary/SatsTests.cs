using Freeside.Core.Monetary;

namespace Freeside.Core.Tests.Monetary;

public sealed class SatsTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(Sats.MaxValue + 1)]
    [InlineData(long.MinValue)]
    public void Sats_outside_zero_to_21_million_btc_are_rejected(long value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sats(value));

    [Theory]
    [InlineData(-1)]
    [InlineData(MilliSats.MaxValue + 1)]
    [InlineData(long.MaxValue)]
    public void MilliSats_outside_zero_to_21_million_btc_are_rejected(long value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new MilliSats(value));

    [Fact]
    public void The_whole_supply_fits_in_millisats() =>
        Assert.Equal(MilliSats.MaxValue, new Sats(Sats.MaxValue).ToMilliSats().Value);

    [Theory]
    [InlineData(1_000, 1, 1, 1)]
    [InlineData(1_001, 1, 2, 1)]
    [InlineData(1_499, 1, 2, 1)]
    [InlineData(1_500, 1, 2, 2)] // tie: 1 is odd, so up to the even 2
    [InlineData(2_500, 2, 3, 2)] // tie: 2 is even, so it stays
    [InlineData(3_500, 3, 4, 4)]
    [InlineData(1_501, 1, 2, 2)]
    [InlineData(0, 0, 0, 0)]
    public void MilliSats_round_to_sats_by_the_named_rule(long msat, long down, long up, long halfEven)
    {
        var amount = new MilliSats(msat);

        Assert.Equal(down, amount.ToSats(RoundingRule.Down).Value);
        Assert.Equal(up, amount.ToSats(RoundingRule.Up).Value);
        Assert.Equal(halfEven, amount.ToSats(RoundingRule.HalfEven).Value);
    }

    [Fact]
    public void An_unnamed_rounding_rule_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new MilliSats(1_500).ToSats(default));

    [Fact]
    public void Amounts_print_without_culture_formatting()
    {
        Assert.Equal("2100000000000000 sat", new Sats(Sats.MaxValue).ToString());
        Assert.Equal("1500 msat", new MilliSats(1_500).ToString());
    }
}
