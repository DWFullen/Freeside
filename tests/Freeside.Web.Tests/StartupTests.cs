using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Freeside.Web.Tests;

public sealed class StartupTests
{
    [Theory]
    [InlineData("regtest")]
    [InlineData("signet")]
    public async Task Healthz_returns_200_when_the_network_is_valid(string network)
    {
        await using var factory = new WebFactory(network);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/healthz", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("testnet3")]
    [InlineData("Mainnet")]
    [InlineData("1")]
    public async Task Web_host_refuses_to_start_when_the_network_is_missing_or_invalid(string? network)
    {
        await using var factory = new WebFactory(network);

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var validation = FindInChain<OptionsValidationException>(ex);
        Assert.NotNull(validation);
        Assert.Contains("Bitcoin:Network", validation.Message, StringComparison.Ordinal);
    }

    private static T? FindInChain<T>(Exception ex)
        where T : Exception => ex switch
        {
            T match => match,
            AggregateException aggregate => aggregate.InnerExceptions.Select(FindInChain<T>).FirstOrDefault(found => found is not null),
            { InnerException: { } inner } => FindInChain<T>(inner),
            _ => null,
        };

    // Sets Bitcoin:Network explicitly (null included) so an ambient Bitcoin__Network environment
    // variable can't leak into these tests.
    private sealed class WebFactory(string? network) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Bitcoin:Network"] = network }));
    }
}
