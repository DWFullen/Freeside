namespace Freeside.Infrastructure.Database;

/// <summary>
/// The <c>Database</c> configuration section. Validated when the host starts; see
/// <see cref="DatabaseServiceCollectionExtensions.AddFreesideDatabase"/>.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// An Npgsql connection string: host, database and username. It may contain a password only
    /// when <see cref="Authentication"/> is <c>Password</c>, which is allowed on regtest only.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Required, with no default: exactly <c>Password</c> or <c>EntraManagedIdentity</c>.</summary>
    public string? Authentication { get; set; }

    /// <summary>
    /// The client ID of a user-assigned managed identity. Leave empty for the system-assigned one.
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }

    public DatabaseAuthentication GetAuthentication() =>
        DatabaseAuthenticationParser.TryParse(Authentication, out var authentication)
            ? authentication
            : throw new InvalidOperationException(DatabaseAuthenticationParser.InvalidMessage);
}

public enum DatabaseAuthentication
{
    /// <summary>A password in the connection string. Regtest only.</summary>
    Password = 1,

    /// <summary>Entra ID tokens from a managed identity, over TLS. Everywhere except regtest.</summary>
    EntraManagedIdentity = 2,
}

internal static class DatabaseAuthenticationParser
{
    public const string InvalidMessage =
        "Database:Authentication must be exactly 'Password' or 'EntraManagedIdentity'.";

    // Exact names only: Enum.TryParse would also accept "password" and "1".
    public static bool TryParse(string? value, out DatabaseAuthentication authentication)
    {
        switch (value)
        {
            case nameof(DatabaseAuthentication.Password):
                authentication = DatabaseAuthentication.Password;
                return true;
            case nameof(DatabaseAuthentication.EntraManagedIdentity):
                authentication = DatabaseAuthentication.EntraManagedIdentity;
                return true;
            default:
                authentication = default;
                return false;
        }
    }
}
