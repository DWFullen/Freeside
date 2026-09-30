using Freeside.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Freeside.Infrastructure.Tests;

/// <summary>
/// One Postgres container for the test run, set up the way an environment is: an admin runs the
/// bootstrap roles script, a non-superuser migrator applies the migrations, and the app logs in
/// as a member of <c>freeside_app</c>. Passwords are random per run.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Same image and digest as app-postgres in tools/regtest/compose.yml (see ImagePinTests).</summary>
    public const string Image = "postgres:18.6@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722";

    public const string MigratorLogin = "migrator_login";
    public const string AppLogin = "app_login";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    public string SuperuserConnectionString => _container.GetConnectionString();

    public string MigratorConnectionString { get; private set; } = "";

    public string AppConnectionString { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var migratorPassword = Guid.NewGuid().ToString("N");
        var appPassword = Guid.NewGuid().ToString("N");
        await ExecuteAsync(SuperuserConnectionString, DatabaseBootstrap.RolesSql);
        await ExecuteAsync(SuperuserConnectionString, $"""
            CREATE ROLE {MigratorLogin} LOGIN PASSWORD '{migratorPassword}' IN ROLE {DatabaseBootstrap.MigratorRole};
            CREATE ROLE {AppLogin} LOGIN PASSWORD '{appPassword}' IN ROLE {DatabaseBootstrap.AppRole};
            """);

        MigratorConnectionString = ConnectionStringFor(MigratorLogin, migratorPassword);
        AppConnectionString = ConnectionStringFor(AppLogin, appPassword);

        await using var db = CreateContext(MigratorConnectionString);
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    public string ConnectionStringFor(string username, string? password) =>
        new NpgsqlConnectionStringBuilder(SuperuserConnectionString) { Username = username, Password = password }.ConnectionString;

    public static FreesideDbContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<FreesideDbContext>();
        FreesideDbContextOptions.Configure(builder, connectionString);
        return new FreesideDbContext(builder.Options);
    }

    public static async Task ExecuteAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    public static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}

[CollectionDefinition(nameof(PostgresDatabase))]
public sealed class PostgresDatabase : ICollectionFixture<PostgresFixture>;
