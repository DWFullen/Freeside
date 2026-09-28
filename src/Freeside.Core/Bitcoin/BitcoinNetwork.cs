namespace Freeside.Core.Bitcoin;

/// <summary>
/// The Bitcoin network an environment is bound to (AGENTS.md §2, invariant 4).
/// </summary>
/// <remarks>
/// There is deliberately no zero member, so <c>default(BitcoinNetwork)</c> is never a valid network.
/// </remarks>
public enum BitcoinNetwork
{
    Regtest = 1,
    Signet = 2,
    Testnet4 = 3,
    Mainnet = 4,
}
