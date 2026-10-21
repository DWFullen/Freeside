using Freeside.Core.Fakes;
using Freeside.Core.Fees;
using Freeside.Core.Fees.Fakes;
using Freeside.Core.Payments.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Freeside.Core.Payments;

public static class PaymentServiceCollectionExtensions
{
    /// <summary>
    /// Registers rail selection and health, and the rails chosen by configuration. The Strike rail
    /// is <see cref="FakeStrikeRail"/> when <c>Payments:Strike:Adapter</c> is <c>Fake</c>, which the
    /// host allows only on regtest, and off when unset. Requires <c>AddBitcoinNetwork</c>.
    /// </summary>
    public static IServiceCollection AddFreesidePayments(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PaymentOptions>()
            .BindConfiguration(PaymentOptions.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PaymentOptions>, PaymentOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<RailHealth>();
        services.TryAddSingleton<RailSelector>();

        if (configuration[$"{PaymentOptions.SectionName}:Strike:Adapter"] == StrikeOptions.FakeAdapter)
        {
            services.AddFake<IPaymentRail, FakeStrikeRail>();
        }

        return services;
    }

    /// <summary>
    /// Registers the fee collector chosen by <c>Fees:Collector:Adapter</c>: <see cref="FakeFeeCollector"/>
    /// for <c>Fake</c> (regtest only), none when unset.
    /// </summary>
    public static IServiceCollection AddFreesideFees(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<FeeOptions>()
            .BindConfiguration(FeeOptions.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<FeeOptions>, FeeOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);

        if (configuration[$"{FeeOptions.SectionName}:Collector:Adapter"] == FeeOptions.FakeAdapter)
        {
            services.AddFake<IFeeCollector, FakeFeeCollector>();
        }

        return services;
    }
}
