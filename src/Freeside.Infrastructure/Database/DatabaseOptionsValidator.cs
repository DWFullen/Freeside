using Freeside.Core.Bitcoin;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Freeside.Infrastructure.Database;

/// <summary>
/// Refuses to start with password authentication on any network but regtest, and requires TLS and
/// no password with Entra (AGENTS.md §2, invariants 4, 8 and 15). Messages never include the
/// connection string, which may hold a password.
/// </summary>
internal sealed class DatabaseOptionsValidator(IOptions<BitcoinOptions> bitcoin) : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        var failures = new List<string>();

        NpgsqlConnectionStringBuilder? connection = null;
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add("Database:ConnectionString is required.");
        }
        else
        {
            try
            {
                connection = new NpgsqlConnectionStringBuilder(options.ConnectionString);
            }
            catch (ArgumentException)
            {
                failures.Add("Database:ConnectionString is not a valid Npgsql connection string.");
            }
        }

        if (connection is not null)
        {
            if (string.IsNullOrWhiteSpace(connection.Host)
                || string.IsNullOrWhiteSpace(connection.Database)
                || string.IsNullOrWhiteSpace(connection.Username))
            {
                failures.Add("Database:ConnectionString must set Host, Database and Username.");
            }
        }

        if (!DatabaseAuthenticationParser.TryParse(options.Authentication, out var authentication))
        {
            failures.Add(DatabaseAuthenticationParser.InvalidMessage);
        }
        else if (connection is not null)
        {
            var hasPassword = !string.IsNullOrEmpty(connection.Password);
            switch (authentication)
            {
                case DatabaseAuthentication.Password:
                    if (NetworkOrNull() is { } network && network != BitcoinNetwork.Regtest)
                    {
                        failures.Add($"Password authentication is allowed only on regtest, but Bitcoin:Network is '{network.ToString().ToLowerInvariant()}'. Use EntraManagedIdentity.");
                    }

                    if (!hasPassword)
                    {
                        failures.Add("Password authentication needs a Password in Database:ConnectionString.");
                    }

                    break;

                case DatabaseAuthentication.EntraManagedIdentity:
                    if (hasPassword)
                    {
                        failures.Add("With EntraManagedIdentity, Database:ConnectionString must not contain a Password.");
                    }

                    if (connection.SslMode is not (SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull))
                    {
                        failures.Add("With EntraManagedIdentity, Database:ConnectionString must set SSL Mode to Require, VerifyCA or VerifyFull.");
                    }

                    break;
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    // A missing or invalid network is reported by the Bitcoin options validator.
    private BitcoinNetwork? NetworkOrNull()
    {
        try
        {
            return BitcoinNetworkParser.TryParse(bitcoin.Value.Network, out var network) ? network : null;
        }
        catch (OptionsValidationException)
        {
            return null;
        }
    }
}
