using Azure.Core;
using Freeside.Infrastructure.Database;
using Npgsql;

namespace Freeside.Infrastructure.Tests;

/// <summary>
/// The Entra path: the data source asks the credential for a Postgres token and logs in with it as
/// the password. Here the "token" is the password of a throwaway role. The container has no TLS, so
/// this calls the builder directly; the TLS requirement is covered by <see cref="DatabaseStartupTests"/>.
/// </summary>
[Collection(nameof(PostgresDatabase))]
public sealed class EntraTokenTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_data_source_logs_in_with_a_token_from_the_credential()
    {
        var token = Guid.NewGuid().ToString("N");
        await PostgresFixture.ExecuteAsync(postgres.SuperuserConnectionString,
            $"CREATE ROLE entra_principal LOGIN PASSWORD '{token}' IN ROLE {DatabaseBootstrap.AppRole}");
        var credential = new FakeTokenCredential(token);
        var options = new DatabaseOptions
        {
            ConnectionString = postgres.ConnectionStringFor("entra_principal", password: null),
            Authentication = "EntraManagedIdentity",
        };

        await using var dataSource = FreesideDataSource.Create(options, () => credential, loggerFactory: null);
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT current_user", connection);

        Assert.Equal("entra_principal", await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        Assert.Contains(FreesideDataSource.EntraScope, credential.RequestedScopes);
    }

    private sealed class FakeTokenCredential(string token) : TokenCredential
    {
        public List<string> RequestedScopes { get; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            RequestedScopes.AddRange(requestContext.Scopes);
            return new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
