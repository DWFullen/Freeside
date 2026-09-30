using Freeside.Core.Bitcoin;
using Freeside.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Freeside.Infrastructure.Tests;

/// <summary>
/// Password authentication is allowed on regtest only; everywhere else it's Entra over TLS
/// (docs/plans/phase-0.md, PR 4). Checked when the host starts, before any connection is made.
/// </summary>
public sealed class DatabaseStartupTests
{
    private const string _password = "not-a-real-password-7f3a";
    private const string _withPassword = $"Host=db.example;Database=freeside;Username=freeside_app;Password={_password}";
    private const string _entra = "Host=db.example;Database=freeside;Username=id-freeside-dev-web;SSL Mode=VerifyFull";

    [Theory]
    [InlineData("regtest", _withPassword, "Password")]
    [InlineData("signet", _entra, "EntraManagedIdentity")]
    [InlineData("mainnet", "Host=db.example;Database=freeside;Username=id;SSL Mode=Require", "EntraManagedIdentity")]
    public async Task The_host_starts_with_an_allowed_combination(string network, string connectionString, string authentication)
    {
        using var host = Build(network, connectionString, authentication);

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("signet", _withPassword, "Password", "allowed only on regtest")]
    [InlineData("mainnet", _withPassword, "Password", "allowed only on regtest")]
    [InlineData("regtest", "Host=db.example;Database=freeside;Username=freeside_app", "Password", "needs a Password")]
    [InlineData("signet", _entra + $";Password={_password}", "EntraManagedIdentity", "must not contain a Password")]
    [InlineData("signet", "Host=db.example;Database=freeside;Username=id", "EntraManagedIdentity", "SSL Mode")]
    [InlineData("signet", "Host=db.example;Database=freeside;Username=id;SSL Mode=Prefer", "EntraManagedIdentity", "SSL Mode")]
    [InlineData("regtest", _withPassword, null, "must be exactly")]
    [InlineData("regtest", _withPassword, "password", "must be exactly")]
    [InlineData("regtest", _withPassword, "1", "must be exactly")]
    [InlineData("regtest", null, "Password", "ConnectionString is required")]
    [InlineData("regtest", "Host=db.example;Username=freeside_app;Password=x", "Password", "must set Host, Database and Username")]
    [InlineData("regtest", "Not A Setting=1", "Password", "not a valid")]
    public async Task The_host_refuses_to_start_otherwise(string network, string? connectionString, string? authentication, string expected)
    {
        using var host = Build(network, connectionString, authentication);

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_password, ex.Message, StringComparison.Ordinal);
    }

    private static IHost Build(string network, string? connectionString, string? authentication)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bitcoin:Network"] = network,
            ["Database:ConnectionString"] = connectionString,
            ["Database:Authentication"] = authentication,
        });
        builder.Services.AddBitcoinNetwork().AddFreesideDatabase();
        return builder.Build();
    }
}
