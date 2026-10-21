using Freeside.Core.Bitcoin;
using Freeside.Core.Fees;
using Freeside.Core.Fees.Fakes;
using Freeside.Core.Payments;
using Freeside.Core.Payments.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Freeside.Core.Tests.Payments;

/// <summary>The placeholder policy: fakes are chosen by configuration, and allowed only on regtest.</summary>
public sealed class PaymentRegistrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Fake_adapters_are_registered_on_regtest()
    {
        using var host = BuildHost("regtest", strike: "Fake", fees: "Fake");

        await host.StartAsync(Ct);

        Assert.IsType<FakeStrikeRail>(Assert.Single(host.Services.GetServices<IPaymentRail>()));
        Assert.IsType<FakeFeeCollector>(host.Services.GetRequiredService<IFeeCollector>());
        Assert.NotNull(host.Services.GetRequiredService<RailSelector>());
        await host.StopAsync(Ct);
    }

    [Theory]
    [InlineData("Fake", null)]
    [InlineData(null, "Fake")]
    public async Task Fake_adapters_are_refused_on_signet(string? strike, string? fees)
    {
        using var host = BuildHost("signet", strike, fees);

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(Ct));

        Assert.Contains("Fake services are allowed only when Bitcoin:Network is regtest", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_adapters_there_are_no_rails_and_no_collector()
    {
        using var host = BuildHost("signet", strike: null, fees: null);

        await host.StartAsync(Ct);

        Assert.Empty(host.Services.GetServices<IPaymentRail>());
        Assert.Null(host.Services.GetService<IFeeCollector>());
        await host.StopAsync(Ct);
    }

    [Theory]
    [InlineData("Strike", null, "Payments:Strike:Adapter")]
    [InlineData(null, "Ach", "Fees:Collector:Adapter")]
    public async Task A_real_adapter_is_refused_until_it_exists(string? strike, string? fees, string expected)
    {
        using var host = BuildHost("regtest", strike, fees);

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(Ct));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Payments:Breaker:FailureThreshold", "0")]
    [InlineData("Payments:Breaker:Cooldown", "00:00:00")]
    [InlineData("Payments:OnChainMinimumUsdCents", "-1")]
    [InlineData("Payments:Rails:Not A Rail:Disabled", "true")]
    public async Task Invalid_payment_settings_are_refused(string key, string value)
    {
        using var host = BuildHost("regtest", null, null, (key, value));

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(Ct));

        Assert.Contains("Payments:", ex.Message, StringComparison.Ordinal);
    }

    private static IHost BuildHost(string network, string? strike, string? fees, (string Key, string Value)? extra = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        var settings = new Dictionary<string, string?>
        {
            ["Bitcoin:Network"] = network,
            ["Payments:Strike:Adapter"] = strike,
            ["Fees:Collector:Adapter"] = fees,
        };
        if (extra is var (key, value))
        {
            settings[key] = value;
        }

        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddBitcoinNetwork();
        builder.Services.AddFreesidePayments(builder.Configuration);
        builder.Services.AddFreesideFees(builder.Configuration);
        return builder.Build();
    }
}
