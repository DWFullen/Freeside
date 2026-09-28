namespace Freeside.Core.Bitcoin;

/// <summary>
/// Parses the <c>Bitcoin:Network</c> setting. Only the exact lowercase names are accepted: no other
/// casing, no enum ordinals (which the configuration binder would otherwise accept), no aliases
/// such as "testnet" or the deprecated "testnet3".
/// </summary>
public static class BitcoinNetworkParser
{
    public static bool TryParse(string? value, out BitcoinNetwork network)
    {
        network = value switch
        {
            "regtest" => BitcoinNetwork.Regtest,
            "signet" => BitcoinNetwork.Signet,
            "testnet4" => BitcoinNetwork.Testnet4,
            "mainnet" => BitcoinNetwork.Mainnet,
            _ => default,
        };
        return network != default;
    }
}
