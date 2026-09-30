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
        await using var factory = new WebFactory(network, withDatabase: true);
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
        await using var factory = new WebFactory(network, withDatabase: true);

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var validation = FindInChain<OptionsValidationException>(ex);
        Assert.NotNull(validation);
        Assert.Contains("Bitcoin:Network", validation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Web_host_refuses_to_start_without_database_configuration()
    {
        await using var factory = new WebFactory("regtest", withDatabase: false);

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var validation = FindInChain<OptionsValidationException>(ex);
        Assert.NotNull(validation);
        Assert.Contains("Database:ConnectionString", validation.Message, StringComparison.Ordinal);
    }

    private static T? FindInChain<T>(Exception ex)
        where T : Exception => ex switch
        {
            T match => match,
            AggregateException aggregate => aggregate.InnerExceptions.Select(FindInChain<T>).FirstOrDefault(found => found is not null),
            { InnerException: { } inner } => FindInChain<T>(inner),
            _ => null,
        };

    // Sets Bitcoin:Network and Database explicitly (nulls included) so ambient Bitcoin__Network
    // or Database__* environment variables can't leak into these tests. Nothing listens on port 1:
    // the host must start, and serve /healthz, without connecting to the database.
    private sealed class WebFactory(string? network, bool withDatabase) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var entra = network is not (null or "regtest");
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Bitcoin:Network"] = network,
                    ["Database:ConnectionString"] = withDatabase
                        ? entra
                            ? "Host=127.0.0.1;Port=1;Database=freeside;Username=freeside-web;SSL Mode=Require"
                            : "Host=127.0.0.1;Port=1;Database=freeside;Username=freeside_app_login;Password=unused"
                        : null,
                    ["Database:Authentication"] = withDatabase ? (entra ? "EntraManagedIdentity" : "Password") : null,
                }));
        }
    }
}
