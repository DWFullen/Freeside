using Freeside.Core.Bitcoin;

namespace Freeside.Core.Tests;

public sealed class BitcoinNetworkParserTests
{
    [Theory]
    [InlineData("regtest", BitcoinNetwork.Regtest)]
    [InlineData("signet", BitcoinNetwork.Signet)]
    [InlineData("testnet4", BitcoinNetwork.Testnet4)]
    [InlineData("mainnet", BitcoinNetwork.Mainnet)]
    public void Accepts_the_four_exact_names(string value, BitcoinNetwork expected)
    {
        Assert.True(BitcoinNetworkParser.TryParse(value, out var network));
        Assert.Equal(expected, network);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Mainnet")]
    [InlineData("REGTEST")]
    [InlineData(" regtest")]
    [InlineData("testnet")]
    [InlineData("testnet3")]
    [InlineData("main")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("4")]
    public void Rejects_anything_else(string? value)
    {
        Assert.False(BitcoinNetworkParser.TryParse(value, out var network));
        Assert.Equal(default, network);
    }

    [Fact]
    public void Default_is_not_a_defined_network() =>
        Assert.False(Enum.IsDefined(default(BitcoinNetwork)));
}
