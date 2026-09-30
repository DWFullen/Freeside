using Azure.Core;
using Freeside.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Freeside.Worker.Tests;

public sealed class StartupTests
{
    // Nothing listens on port 1: the worker must start without connecting. With no handlers
    // registered yet, the queue processors don't poll.
    private const string _passwordConnection = "Host=127.0.0.1;Port=1;Database=freeside;Username=freeside_app_login;Password=unused";
    private const string _entraConnection = "Host=127.0.0.1;Port=1;Database=freeside;Username=freeside-worker;SSL Mode=Require";

    [Theory]
    [InlineData("regtest")]
    [InlineData("signet")]
    public async Task Worker_starts_and_stops_when_the_network_is_valid(string network)
    {
        using var host = BuildHost(network, withDatabase: true);

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
        using var host = BuildHost(network, withDatabase: true);

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Bitcoin:Network", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Worker_refuses_to_start_without_database_configuration()
    {
        using var host = BuildHost("regtest", withDatabase: false);

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Database:ConnectionString", ex.Message, StringComparison.Ordinal);
    }

    // Same defaults as Program. The in-memory source is added last, so it overrides any ambient
    // Bitcoin__Network or Database__* environment variable (null values included). Signet uses
    // Entra, with a stand-in credential so nothing calls the managed identity endpoint.
    private static IHost BuildHost(string? network, bool withDatabase)
    {
        var entra = network is not (null or "regtest");
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bitcoin:Network"] = network,
            ["Database:ConnectionString"] = withDatabase ? (entra ? _entraConnection : _passwordConnection) : null,
            ["Database:Authentication"] = withDatabase ? (entra ? "EntraManagedIdentity" : "Password") : null,
        });
        builder.Services.AddKeyedSingleton<TokenCredential>(DatabaseServiceCollectionExtensions.TokenCredentialKey, new NoTokenCredential());
        WorkerServices.Configure(builder.Services, builder.Configuration);
        return builder.Build();
    }

    private sealed class NoTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The worker must not connect to the database at startup.");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The worker must not connect to the database at startup.");
    }
}
