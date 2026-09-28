using Freeside.Core.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Freeside.Core.Bitcoin;

public static class BitcoinServiceCollectionExtensions
{
    /// <summary>
    /// Binds <c>Bitcoin:Network</c> and validates it when the host starts. The host refuses to start
    /// if the setting is missing or invalid, or if any fake is registered while the network is not
    /// regtest (AGENTS.md §2, invariants 4 and 15).
    /// </summary>
    public static IServiceCollection AddBitcoinNetwork(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<BitcoinOptions>()
            .BindConfiguration(BitcoinOptions.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BitcoinOptions>, BitcoinOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BitcoinOptions>>(new FakeServiceGuard(services)));
        return services;
    }
}
