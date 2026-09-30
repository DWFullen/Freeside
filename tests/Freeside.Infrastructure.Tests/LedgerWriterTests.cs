using Freeside.Core.Bitcoin;
using Freeside.Core.Ledger;
using Freeside.Core.Monetary;
using Freeside.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Freeside.Infrastructure.Tests;

[Collection(nameof(PostgresDatabase))]
public sealed class LedgerWriterTests(PostgresFixture postgres)
{
    [Fact]
    public async Task An_entry_is_written_through_the_registered_services_and_reads_back_unchanged()
    {
        await using var services = Services(postgres.AppConnectionString);
        await using var scope = services.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<ILedgerWriter>();
        var entry = LedgerEntries.Valid();

        var written = await writer.AppendAsync(entry, TestContext.Current.CancellationToken);

        Assert.True(written.Id > 0);
        Assert.Equal(TimeSpan.Zero, written.RecordedAt.Offset);
        Assert.InRange(written.RecordedAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));

        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var read = await db.LedgerEntries.AsNoTracking().SingleAsync(e => e.Id == written.Id, TestContext.Current.CancellationToken);
        Assert.Equal(entry.OccurredAt, read.OccurredAt);
        Assert.Equal(entry.SubjectId, read.SubjectId);
        Assert.Equal(entry.PreviousState, read.PreviousState);
        Assert.Equal(entry.NextState, read.NextState);
        Assert.Equal(new MilliSats(153_846_154), read.Amount);
        Assert.Equal(new Money(10_000, Currency.Usd), read.FiatAmount);
        Assert.Equal(entry.CorrelationId, read.CorrelationId);
        Assert.Null(read.Reason);
    }

    [Fact]
    public async Task A_second_entry_with_the_same_idempotency_key_is_rejected()
    {
        var key = "btcpay:inv_" + Guid.NewGuid().ToString("N") + ":Paid";
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var writer = new Ledger.EfLedgerWriter(db);
        await writer.AppendAsync(LedgerEntries.Valid(key), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DuplicateLedgerEntryException>(() =>
            writer.AppendAsync(LedgerEntries.Valid(key), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_invalid_entry_is_rejected_before_anything_is_written()
    {
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var entry = new LedgerEntry
        {
            OccurredAt = new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.FromHours(-4)),
            EntryType = " order.state",
            SubjectType = "order",
            SubjectId = "ord_1",
            Source = "",
            Actor = "system",
            CorrelationId = "corr",
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            new Ledger.EfLedgerWriter(db).AppendAsync(entry, TestContext.Current.CancellationToken));

        Assert.Contains("OccurredAt must be UTC", ex.Message, StringComparison.Ordinal);
        Assert.Contains("EntryType must not be empty or have leading or trailing whitespace", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Source is required", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-1", "NULL", "NULL")] // negative msat
    [InlineData("NULL", "-1", "'USD'")] // negative fiat
    [InlineData("NULL", "100", "NULL")] // fiat amount without currency
    [InlineData("NULL", "NULL", "'USD'")] // currency without amount
    [InlineData("NULL", "100", "'usd'")] // not an ISO 4217 code
    public async Task Check_constraints_reject_bad_amounts_written_directly(string msat, string fiat, string currency)
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => PostgresFixture.ExecuteAsync(postgres.AppConnectionString, $"""
            INSERT INTO ledger_entries (occurred_at, entry_type, subject_type, subject_id, source, actor, correlation_id, amount_msat, fiat_amount_minor, fiat_currency)
            VALUES (now(), 'order.state', 'order', 'ord_x', 'test', 'system', 'corr', {msat}, {fiat}, {currency})
            """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    private static ServiceProvider Services(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bitcoin:Network"] = "regtest",
                ["Database:ConnectionString"] = connectionString,
                ["Database:Authentication"] = "Password",
            })
            .Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddBitcoinNetwork()
            .AddFreesideDatabase()
            .BuildServiceProvider(validateScopes: true);
    }
}
