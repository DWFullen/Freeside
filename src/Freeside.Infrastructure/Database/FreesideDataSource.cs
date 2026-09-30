using Azure.Core;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Freeside.Infrastructure.Database;

internal static class FreesideDataSource
{
    /// <summary>The token audience for Azure Database for PostgreSQL.</summary>
    public const string EntraScope = "https://ossrdbms-aad.database.windows.net/.default";

    /// <summary>
    /// Builds the data source. With Entra, each new physical connection uses a fresh-enough token
    /// from <paramref name="credential"/> as its password. Options must already be validated.
    /// </summary>
    public static NpgsqlDataSource Create(DatabaseOptions options, Func<TokenCredential> credential, ILoggerFactory? loggerFactory)
    {
        var builder = new NpgsqlDataSourceBuilder(options.ConnectionString);
        if (loggerFactory is not null)
        {
            builder.UseLoggerFactory(loggerFactory);
        }

        if (options.GetAuthentication() == DatabaseAuthentication.EntraManagedIdentity)
        {
            var tokens = credential();
            builder.UsePeriodicPasswordProvider(
                async (_, cancellationToken) =>
                    (await tokens.GetTokenAsync(new TokenRequestContext([EntraScope]), cancellationToken).ConfigureAwait(false)).Token,
                successRefreshInterval: TimeSpan.FromMinutes(30),
                failureRefreshInterval: TimeSpan.FromSeconds(10));
        }

        return builder.Build();
    }
}
