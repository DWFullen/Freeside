namespace Freeside.Infrastructure.Database;

/// <summary>SQL an admin runs once per database before the first migration.</summary>
public static class DatabaseBootstrap
{
    /// <summary>The group role that owns the tables and applies migrations.</summary>
    public const string MigratorRole = "freeside_migrator";

    /// <summary>The group role the web and worker hosts run as.</summary>
    public const string AppRole = "freeside_app";

    /// <summary>Creates <see cref="MigratorRole"/> and <see cref="AppRole"/> (Database/bootstrap-roles.sql).</summary>
    public static string RolesSql { get; } = ReadResource("Freeside.Infrastructure.Database.bootstrap-roles.sql");

    private static string ReadResource(string name)
    {
        using var stream = typeof(DatabaseBootstrap).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
