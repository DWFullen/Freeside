namespace Freeside.Infrastructure.Tests;

[Collection(nameof(PostgresDatabase))]
public sealed class SchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task No_column_in_the_database_is_floating_point() =>
        Assert.Equal(0L, await PostgresFixture.ScalarAsync<long>(postgres.SuperuserConnectionString, """
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = 'public' AND data_type IN ('real', 'double precision')
            """));

    [Theory]
    [InlineData("amount_msat", "bigint")]
    [InlineData("fiat_amount_minor", "bigint")]
    [InlineData("occurred_at", "timestamp with time zone")]
    [InlineData("recorded_at", "timestamp with time zone")]
    public async Task Ledger_columns_have_the_expected_types(string column, string type) =>
        Assert.Equal(type, await PostgresFixture.ScalarAsync<string>(postgres.SuperuserConnectionString, $"""
            SELECT data_type FROM information_schema.columns
            WHERE table_name = 'ledger_entries' AND column_name = '{column}'
            """));
}
