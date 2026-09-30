using Freeside.Core.Ledger;
using Freeside.Infrastructure.Ledger;
using Freeside.Infrastructure.Messaging;
using Freeside.Infrastructure.Persistence;
using Npgsql;

namespace Freeside.Infrastructure.Tests.Messaging;

[Collection(nameof(PostgresDatabase))]
public sealed class QueueWriterTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_job_with_a_dedupe_key_that_exists_is_not_scheduled_again()
    {
        var type = Queues.UniqueKey("test.dedupe");
        var key = Queues.UniqueKey("reconcile");
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var scheduler = new EfJobScheduler(db, Queues.Settings());

        var first = await scheduler.ScheduleAsync(type, "{}", dedupeKey: key, cancellationToken: Ct);
        var second = await scheduler.ScheduleAsync(type, """{"other":true}""", dedupeKey: key, cancellationToken: Ct);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(1L, await Queues.CountAsync(postgres, "jobs WHERE dedupe_key = @key", ("key", key)));
    }

    [Fact]
    public async Task A_job_payload_must_be_JSON()
    {
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            new EfJobScheduler(db, Queues.Settings()).ScheduleAsync(Queues.UniqueKey("test.json"), "not json", cancellationToken: Ct));

        Assert.Equal(PostgresErrorCodes.InvalidTextRepresentation, ex.SqlState);
    }

    [Fact]
    public async Task A_job_run_after_time_must_be_UTC()
    {
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var runAfter = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-5));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new EfJobScheduler(db, Queues.Settings()).ScheduleAsync(Queues.UniqueKey("test.utc"), "{}", runAfter, cancellationToken: Ct));
    }

    [Fact]
    public async Task The_inbox_ignores_a_duplicate_delivery_from_the_same_source()
    {
        var source = Queues.UniqueKey("test.btcpay");
        var otherSource = Queues.UniqueKey("test.strike");
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var inbox = new EfInbox(db, Queues.Settings());

        Assert.True(await inbox.AcceptAsync(source, "delivery-1", "InvoiceSettled", "{}", cancellationToken: Ct));
        Assert.False(await inbox.AcceptAsync(source, "delivery-1", "InvoiceSettled", "{}", cancellationToken: Ct));
        Assert.True(await inbox.AcceptAsync(otherSource, "delivery-1", "invoice.updated", "{}", cancellationToken: Ct));

        Assert.Equal(1L, await Queues.CountAsync(postgres, "inbox_messages WHERE source = @source", ("source", source)));
        Assert.Equal(1L, await Queues.CountAsync(postgres, "inbox_messages WHERE source = @source", ("source", otherSource)));
    }

    [Theory]
    [InlineData(" ", "delivery-1", "type")]
    [InlineData("source", "", "type")]
    [InlineData("source", "delivery-1 ", "type")]
    [InlineData("source", "delivery-1", "")]
    public async Task The_inbox_rejects_blank_or_padded_keys(string source, string dedupeKey, string messageType)
    {
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            new EfInbox(db, Queues.Settings()).AcceptAsync(source, dedupeKey, messageType, "{}", cancellationToken: Ct));
    }

    [Fact]
    public async Task The_inbox_refuses_a_payload_over_the_limit()
    {
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);

        await Assert.ThrowsAsync<ArgumentException>(() => new EfInbox(db, Queues.Settings()).AcceptAsync(
            Queues.UniqueKey("test.big"), "delivery-1", "type", new string('x', EfInbox.MaxPayloadLength + 1), cancellationToken: Ct));
    }

    [Fact]
    public async Task Outbox_messages_and_ledger_entries_roll_back_with_the_unit_of_work()
    {
        var type = Queues.UniqueKey("test.outbox-rollback");
        var entry = LedgerEntries.Valid();
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var outbox = new EfOutbox(db, Queues.Settings());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new EfUnitOfWork(db).ExecuteAsync(async ct =>
        {
            outbox.Add(type, """{"receipt":1}""", entry.CorrelationId);

            // Saves inside the transaction, so the outbox row is written too, then rolled back.
            await new EfLedgerWriter(db).AppendAsync(entry, ct);
            outbox.Add(type, """{"receipt":2}""", entry.CorrelationId);
            throw new InvalidOperationException("The caller's change failed.");
        }, Ct));

        Assert.Equal("The caller's change failed.", ex.Message);
        await db.SaveChangesAsync(Ct);
        Assert.Equal(0L, await Queues.CountAsync(postgres, "outbox_messages WHERE message_type = @type", ("type", type)));
        Assert.Equal(0L, await Queues.CountAsync(postgres, "ledger_entries WHERE correlation_id = @id", ("id", entry.CorrelationId)));
    }

    [Fact]
    public async Task Outbox_messages_and_ledger_entries_commit_together()
    {
        var type = Queues.UniqueKey("test.outbox-commit");
        var entry = LedgerEntries.Valid();
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var outbox = new EfOutbox(db, Queues.Settings());

        await new EfUnitOfWork(db).ExecuteAsync(async ct =>
        {
            await new EfLedgerWriter(db).AppendAsync(entry, ct);
            outbox.Add(type, """{"receipt":1}""", entry.CorrelationId);
        }, Ct);

        Assert.Equal(1L, await Queues.CountAsync(postgres,
            "outbox_messages WHERE message_type = @type AND status = 'Pending' AND correlation_id = @id", ("type", type), ("id", entry.CorrelationId)));
        Assert.Equal(1L, await Queues.CountAsync(postgres, "ledger_entries WHERE correlation_id = @id", ("id", entry.CorrelationId)));
    }

    [Fact]
    public async Task Units_of_work_do_not_nest()
    {
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var unitOfWork = new EfUnitOfWork(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteAsync(ct => unitOfWork.ExecuteAsync(_ => Task.CompletedTask, ct), Ct));
    }

    public static TheoryData<string, string, bool> Privileges()
    {
        var data = new TheoryData<string, string, bool>();
        foreach (var table in new[] { "jobs", "inbox_messages", "outbox_messages" })
        {
            data.Add(table, "SELECT", true);
            data.Add(table, "INSERT", true);
            data.Add(table, "UPDATE", true);
            data.Add(table, "DELETE", false);
            data.Add(table, "TRUNCATE", false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Privileges))]
    public async Task The_app_role_can_claim_and_settle_rows_but_not_delete_them(string table, string privilege, bool expected) =>
        Assert.Equal(expected, await Queues.ScalarAsync<bool>(postgres,
            $"SELECT has_table_privilege('{PostgresFixture.AppLogin}', '{table}', '{privilege}')"));
}
