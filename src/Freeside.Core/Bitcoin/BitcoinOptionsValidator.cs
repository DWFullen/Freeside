using Microsoft.Extensions.Options;

namespace Freeside.Core.Bitcoin;

internal sealed class BitcoinOptionsValidator : IValidateOptions<BitcoinOptions>
{
    public ValidateOptionsResult Validate(string? name, BitcoinOptions options) =>
        BitcoinNetworkParser.TryParse(options.Network, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(InvalidNetworkMessage(options.Network));

    internal static string InvalidNetworkMessage(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "Bitcoin:Network is required and has no default. Set it to one of: regtest, signet, testnet4, mainnet."
            : $"Bitcoin:Network '{value}' is not valid. Use exactly one of: regtest, signet, testnet4, mainnet.";
}
