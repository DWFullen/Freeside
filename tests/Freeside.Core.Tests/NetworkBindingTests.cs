using Freeside.Core.Bitcoin;
using Freeside.Core.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Freeside.Core.Tests;

public sealed class NetworkBindingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("testnet3")]
    [InlineData("Mainnet")]
    [InlineData("1")]
    public async Task Host_refuses_to_start_when_network_is_missing_or_invalid(string? network)
    {
        using var host = BuildHost(network);

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Bitcoin:Network", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("regtest", BitcoinNetwork.Regtest)]
    [InlineData("signet", BitcoinNetwork.Signet)]
    public async Task Host_starts_and_exposes_the_bound_network(string value, BitcoinNetwork expected)
    {
        using var host = BuildHost(value);

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, host.Services.GetRequiredService<IOptions<BitcoinOptions>>().Value.GetNetwork());
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Fakes_are_allowed_on_regtest()
    {
        using var host = BuildHost("regtest", services => services.AddFake<IClockForTests, FakeClockForTests>());

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.IsType<FakeClockForTests>(host.Services.GetRequiredService<IClockForTests>());
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("signet")]
    [InlineData("testnet4")]
    public async Task Host_refuses_to_start_with_a_fake_on_any_other_network(string network)
    {
        using var host = BuildHost(network, services => services.AddFake<IClockForTests, FakeClockForTests>());

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("allowed only when Bitcoin:Network is regtest", ex.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(FakeClockForTests).FullName!, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Guard_sees_keyed_fakes_and_fakes_registered_after_the_network()
    {
        using var host = BuildHost("signet", services =>
            services.AddKeyedSingleton<IClockForTests, FakeClockForTests>("backup"));

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains(typeof(FakeClockForTests).FullName!, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Real_implementations_are_allowed_on_other_networks()
    {
        using var host = BuildHost("signet", services => services.AddSingleton<IClockForTests, SystemClockForTests>());

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.IsType<SystemClockForTests>(host.Services.GetRequiredService<IClockForTests>());
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    // The configuration key is set explicitly (null included) so an ambient Bitcoin__Network
    // environment variable can't leak into these tests.
    private static IHost BuildHost(string? network, Action<IServiceCollection>? configure = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Bitcoin:Network"] = network });
        builder.Services.AddBitcoinNetwork();
        configure?.Invoke(builder.Services);
        return builder.Build();
    }

    private interface IClockForTests
    {
        DateTimeOffset UtcNow { get; }
    }

    private sealed class FakeClockForTests : IClockForTests, IFakeService
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class SystemClockForTests : IClockForTests
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
