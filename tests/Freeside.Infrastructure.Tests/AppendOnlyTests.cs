using Freeside.Core.Ledger;
using Npgsql;

namespace Freeside.Infrastructure.Tests;

/// <summary>AGENTS.md §2, invariant 7: the ledger is append-only, enforced by the database.</summary>
[Collection(nameof(PostgresDatabase))]
public sealed class AppendOnlyTests(PostgresFixture postgres)
{
    public static TheoryData<string> Mutations => new()
    {
        "UPDATE ledger_entries SET actor = 'tampered' WHERE id = @id",
        "DELETE FROM ledger_entries WHERE id = @id",
        "TRUNCATE ledger_entries",
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task The_app_role_lacks_the_privilege_to_change_entries(string sql)
    {
        var id = await AppendAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            PostgresFixture.ExecuteAsync(postgres.AppConnectionString, sql, ("id", id)));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Triggers_stop_even_the_table_owner(string sql)
    {
        var id = await AppendAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            PostgresFixture.ExecuteAsync(postgres.MigratorConnectionString, sql, ("id", id)));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, ex.SqlState);
        Assert.Contains("append-only", ex.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_the_triggers_disabled_the_app_role_still_cannot_update()
    {
        var id = await AppendAsync();
        await PostgresFixture.ExecuteAsync(postgres.SuperuserConnectionString, "ALTER TABLE ledger_entries DISABLE TRIGGER USER");
        try
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() => PostgresFixture.ExecuteAsync(
                postgres.AppConnectionString, "UPDATE ledger_entries SET actor = 'tampered' WHERE id = @id", ("id", id)));

            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
        }
        finally
        {
            await PostgresFixture.ExecuteAsync(postgres.SuperuserConnectionString, "ALTER TABLE ledger_entries ENABLE TRIGGER USER");
        }
    }

    [Theory]
    [InlineData("SELECT", true)]
    [InlineData("INSERT", true)]
    [InlineData("UPDATE", false)]
    [InlineData("DELETE", false)]
    [InlineData("TRUNCATE", false)]
    [InlineData("REFERENCES", false)]
    [InlineData("TRIGGER", false)]
    public async Task The_app_role_holds_only_select_and_insert(string privilege, bool expected) =>
        Assert.Equal(expected, await PostgresFixture.ScalarAsync<bool>(postgres.SuperuserConnectionString,
            $"SELECT has_table_privilege('{PostgresFixture.AppLogin}', 'ledger_entries', '{privilege}')"));

    private async Task<long> AppendAsync()
    {
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var entry = await new Ledger.EfLedgerWriter(db).AppendAsync(LedgerEntries.Valid(), TestContext.Current.CancellationToken);
        return entry.Id;
    }
}
