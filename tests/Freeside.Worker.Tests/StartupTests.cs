using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Freeside.Worker.Tests;

public sealed class StartupTests
{
    [Theory]
    [InlineData("regtest")]
    [InlineData("signet")]
    public async Task Worker_starts_and_stops_when_the_network_is_valid(string network)
    {
        using var host = BuildHost(network);

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("testnet3")]
    [InlineData("Mainnet")]
    [InlineData("1")]
    public async Task Worker_refuses_to_start_when_the_network_is_missing_or_invalid(string? network)
    {
        using var host = BuildHost(network);

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Bitcoin:Network", ex.Message, StringComparison.Ordinal);
    }

    // Same defaults as Program. The in-memory source is added last, so it overrides any ambient
    // Bitcoin__Network environment variable (a null value included).
    private static IHost BuildHost(string? network)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Bitcoin:Network"] = network });
        WorkerServices.Configure(builder.Services);
        return builder.Build();
    }
}
