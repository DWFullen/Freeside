namespace Freeside.Core.Bitcoin;

/// <summary>
/// The <c>Bitcoin</c> configuration section. Validated when the host starts; see
/// <see cref="BitcoinServiceCollectionExtensions.AddBitcoinNetwork"/>.
/// </summary>
public sealed class BitcoinOptions
{
    public const string SectionName = "Bitcoin";

    /// <summary>
    /// Required, with no default: one of <c>regtest</c>, <c>signet</c>, <c>testnet4</c>, <c>mainnet</c>.
    /// </summary>
    public string? Network { get; set; }

    /// <summary>
    /// The parsed network. The host validates the setting at startup, so this only throws if called
    /// on options that bypassed validation.
    /// </summary>
    public BitcoinNetwork GetNetwork() =>
        BitcoinNetworkParser.TryParse(Network, out var network)
            ? network
            : throw new InvalidOperationException(BitcoinOptionsValidator.InvalidNetworkMessage(Network));
}
