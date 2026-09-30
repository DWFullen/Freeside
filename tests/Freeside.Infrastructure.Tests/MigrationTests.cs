using Microsoft.EntityFrameworkCore;

namespace Freeside.Infrastructure.Tests;

[Collection(nameof(PostgresDatabase))]
public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_apply_to_an_empty_database_as_a_non_superuser()
    {
        await using var db = PostgresFixture.CreateContext(postgres.MigratorConnectionString);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken), m => m.EndsWith("_InitialLedger", StringComparison.Ordinal));
        Assert.False(await PostgresFixture.ScalarAsync<bool>(postgres.SuperuserConnectionString,
            $"SELECT rolsuper FROM pg_roles WHERE rolname = '{PostgresFixture.MigratorLogin}'"));
    }

    [Fact]
    public void The_model_has_no_changes_missing_from_the_migrations()
    {
        using var db = new FreesideDesignTimeDbContextFactory().CreateDbContext([]);

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task The_ledger_table_is_owned_by_the_migrator_role() =>
        Assert.Equal("freeside_migrator", await PostgresFixture.ScalarAsync<string>(postgres.SuperuserConnectionString,
            "SELECT tableowner FROM pg_tables WHERE tablename = 'ledger_entries'"));
}
