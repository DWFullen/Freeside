using Freeside.Core.Bitcoin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Freeside.Core.Fakes;

/// <summary>
/// Fails <see cref="BitcoinOptions"/> validation when any <see cref="IFakeService"/> is registered and
/// the network is not regtest. Holds the service collection because the check must see every
/// registration, including those made after <c>AddBitcoinNetwork</c>; it runs when the host starts.
/// </summary>
internal sealed class FakeServiceGuard(IServiceCollection services) : IValidateOptions<BitcoinOptions>
{
    public ValidateOptionsResult Validate(string? name, BitcoinOptions options)
    {
        // A missing or invalid network is reported by BitcoinOptionsValidator.
        if (!BitcoinNetworkParser.TryParse(options.Network, out var network) || network == BitcoinNetwork.Regtest)
        {
            return ValidateOptionsResult.Skip;
        }

        var fakes = services
            .Select(ImplementationType)
            .Where(type => type is not null && typeof(IFakeService).IsAssignableFrom(type))
            .Select(type => type!.FullName)
            .Distinct()
            .ToList();

        return fakes.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Fake services are allowed only when Bitcoin:Network is regtest, but it is '{options.Network}'. " +
                $"Registered fakes: {string.Join(", ", fakes)}.");
    }

    // Keyed descriptors throw if their non-keyed properties are read.
    private static Type? ImplementationType(ServiceDescriptor descriptor) =>
        descriptor.IsKeyedService
            ? descriptor.KeyedImplementationType ?? descriptor.KeyedImplementationInstance?.GetType()
            : descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();
}
