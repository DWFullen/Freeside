using Azure.Core;
using Azure.Identity;
using Freeside.Core.Ledger;
using Freeside.Infrastructure.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Freeside.Infrastructure.Database;

public static class DatabaseServiceCollectionExtensions
{
    /// <summary>Service key of the <see cref="TokenCredential"/> used for Entra database logins.</summary>
    public const string TokenCredentialKey = "Freeside.Database";

    /// <summary>
    /// Binds <c>Database</c>, validates it when the host starts, and registers the data source,
    /// <see cref="FreesideDbContext"/> and <see cref="ILedgerWriter"/>. Requires
    /// <c>AddBitcoinNetwork</c>. Never applies migrations.
    /// </summary>
    public static IServiceCollection AddFreesideDatabase(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>());

        // ManagedIdentityCredential, not DefaultAzureCredential: no fallback to developer sign-ins.
        services.TryAddKeyedSingleton<TokenCredential>(TokenCredentialKey, (provider, _) =>
        {
            var clientId = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ManagedIdentityClientId;
            return new ManagedIdentityCredential(string.IsNullOrWhiteSpace(clientId)
                ? ManagedIdentityId.SystemAssigned
                : ManagedIdentityId.FromUserAssignedClientId(clientId));
        });

        services.TryAddSingleton(provider => FreesideDataSource.Create(
            provider.GetRequiredService<IOptions<DatabaseOptions>>().Value,
            () => provider.GetRequiredKeyedService<TokenCredential>(TokenCredentialKey),
            provider.GetService<ILoggerFactory>()));

        services.AddDbContextPool<FreesideDbContext>((provider, builder) =>
            FreesideDbContextOptions.Configure(builder, provider.GetRequiredService<NpgsqlDataSource>()));

        services.TryAddScoped<ILedgerWriter, EfLedgerWriter>();
        return services;
    }
}
