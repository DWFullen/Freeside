using System.Collections.Concurrent;
using Freeside.Infrastructure.Messaging;
using Npgsql;

namespace Freeside.Infrastructure.Tests.Messaging;

/// <summary>Claiming, leases and settling, against the real SQL (docs/plans/phase-0.md, PR 5).</summary>
[Collection(nameof(PostgresDatabase))]
public sealed class WorkQueueStoreTests(PostgresFixture postgres) : IAsyncDisposable
{
    private static readonly QueueDefinition _jobs = QueueDefinition.Jobs;
    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(5);

    private readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(postgres.AppConnectionString);

    private WorkQueueStore Store => new(_dataSource);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    [Fact]
    public async Task Four_workers_run_each_of_200_jobs_exactly_once()
    {
        var type = Queues.UniqueKey("test.concurrent");
        for (var i = 0; i < 200; i++)
        {
            await Queues.ScheduleAsync(postgres, type);
        }

        var runs = new ConcurrentDictionary<long, int>();
        var claimsPerWorker = new ConcurrentDictionary<string, int>();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(n => Task.Run(async () =>
        {
            var worker = $"worker-{n}";
            while (true)
            {
                var claimed = await Store.ClaimAsync(_jobs, worker, [type], 5, _lease, Ct);
                if (claimed.Count == 0)
                {
                    return;
                }

                claimsPerWorker.AddOrUpdate(worker, claimed.Count, (_, count) => count + claimed.Count);
                foreach (var item in claimed)
                {
                    runs.AddOrUpdate(item.Item.Id, 1, (_, count) => count + 1);
                    Assert.True(await Store.CompleteAsync(_jobs, item.Item.Id, worker, Ct));
                }
            }
        }, Ct)));

        Assert.Equal(200, runs.Count);
        Assert.All(runs.Values, count => Assert.Equal(1, count));
        Assert.Equal(200, claimsPerWorker.Values.Sum());
        Assert.Equal(200L, await Queues.CountAsync(postgres,
            "jobs WHERE job_type = @type AND status = 'Succeeded' AND attempts = 1 AND locked_by IS NULL", ("type", type)));
    }

    [Fact]
    public async Task A_leased_job_is_not_claimed_again_until_the_lease_expires_and_the_old_holder_is_fenced_out()
    {
        var type = Queues.UniqueKey("test.lease");
        var id = await Queues.ScheduleAsync(postgres, type);

        var first = Assert.Single(await Store.ClaimAsync(_jobs, "worker-a", [type], 10, _lease, Ct));
        Assert.Equal(1, first.Item.Attempt);
        Assert.Empty(await Store.ClaimAsync(_jobs, "worker-b", [type], 10, _lease, Ct));

        await Queues.ExpireLeaseAsync(postgres, _jobs, id);
        var second = Assert.Single(await Store.ClaimAsync(_jobs, "worker-b", [type], 10, _lease, Ct));
        Assert.Equal(id, second.Item.Id);
        Assert.Equal(2, second.Item.Attempt);

        // worker-a comes back late: none of its results count.
        Assert.False(await Store.CompleteAsync(_jobs, id, "worker-a", Ct));
        Assert.Equal(FailureOutcome.Fenced, await Store.FailAsync(_jobs, id, "worker-a", "late", TimeSpan.FromMinutes(1), Ct));
        Assert.False(await Store.ReleaseAsync(_jobs, id, "worker-a", Ct));
        Assert.Equal(new QueueRow("Running", 2, "worker-b", null, false, false), await Queues.RowAsync(postgres, _jobs, id));

        Assert.True(await Store.CompleteAsync(_jobs, id, "worker-b", Ct));
        Assert.Equal(new QueueRow("Succeeded", 2, null, null, false, true), await Queues.RowAsync(postgres, _jobs, id));
    }

    [Fact]
    public async Task A_failed_job_backs_off_retries_and_goes_Dead_after_max_attempts()
    {
        var type = Queues.UniqueKey("test.retry");
        var id = await Queues.ScheduleAsync(postgres, type, maxAttempts: 3);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var claimed = Assert.Single(await Store.ClaimAsync(_jobs, "worker", [type], 10, _lease, Ct));
            Assert.Equal(attempt, claimed.Item.Attempt);
            Assert.Equal(FailureOutcome.Retry, await Store.FailAsync(_jobs, id, "worker", $"boom {attempt}", TimeSpan.FromMinutes(1), Ct));
            Assert.Equal(new QueueRow("Pending", attempt, null, $"boom {attempt}", true, false), await Queues.RowAsync(postgres, _jobs, id));

            // Not due until the backoff has passed.
            Assert.Empty(await Store.ClaimAsync(_jobs, "worker", [type], 10, _lease, Ct));
            await Queues.MakeDueAsync(postgres, _jobs, id);
        }

        var last = Assert.Single(await Store.ClaimAsync(_jobs, "worker", [type], 10, _lease, Ct));
        Assert.Equal(3, last.Item.Attempt);
        Assert.Equal(FailureOutcome.Dead, await Store.FailAsync(_jobs, id, "worker", "boom 3", TimeSpan.FromMinutes(1), Ct));
        Assert.Equal(new QueueRow("Dead", 3, null, "boom 3", false, true), await Queues.RowAsync(postgres, _jobs, id));

        await Queues.MakeDueAsync(postgres, _jobs, id);
        Assert.Empty(await Store.ClaimAsync(_jobs, "worker", [type], 10, _lease, Ct));
    }

    [Fact]
    public async Task The_sweep_marks_Dead_a_job_whose_lease_expired_on_its_last_attempt()
    {
        var type = Queues.UniqueKey("test.sweep");
        var id = await Queues.ScheduleAsync(postgres, type, maxAttempts: 1);
        Assert.Single(await Store.ClaimAsync(_jobs, "worker-a", [type], 10, _lease, Ct));

        await Queues.ExpireLeaseAsync(postgres, _jobs, id);

        Assert.Empty(await Store.ClaimAsync(_jobs, "worker-b", [type], 10, _lease, Ct));
        Assert.Contains(id, await Store.SweepAsync(_jobs, Ct));
        var row = await Queues.RowAsync(postgres, _jobs, id);
        Assert.Equal(("Dead", 1, (string?)null, true), (row.Status, row.Attempts, row.LockedBy, row.IsCompleted));
        Assert.Contains("lease expired", row.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Jobs_without_a_handler_in_this_worker_stay_Pending()
    {
        var unhandled = Queues.UniqueKey("test.unhandled");
        var id = await Queues.ScheduleAsync(postgres, unhandled);

        Assert.Empty(await Store.ClaimAsync(_jobs, "worker", [Queues.UniqueKey("test.other")], 10, _lease, Ct));

        Assert.Equal(new QueueRow("Pending", 0, null, null, false, false), await Queues.RowAsync(postgres, _jobs, id));
    }

    [Fact]
    public async Task A_job_is_not_claimed_before_its_run_after_time()
    {
        var type = Queues.UniqueKey("test.later");
        var id = await Queues.ScheduleAsync(postgres, type, runAfter: DateTimeOffset.UtcNow.AddHours(1));

        Assert.Empty(await Store.ClaimAsync(_jobs, "worker", [type], 10, _lease, Ct));

        await Queues.MakeDueAsync(postgres, _jobs, id);
        Assert.Equal(id, Assert.Single(await Store.ClaimAsync(_jobs, "worker", [type], 10, _lease, Ct)).Item.Id);
    }

    [Fact]
    public async Task Releasing_a_claim_does_not_count_the_attempt()
    {
        var type = Queues.UniqueKey("test.release");
        var id = await Queues.ScheduleAsync(postgres, type);
        Assert.Single(await Store.ClaimAsync(_jobs, "worker", [type], 10, _lease, Ct));

        Assert.True(await Store.ReleaseAsync(_jobs, id, "worker", Ct));

        Assert.Equal(new QueueRow("Pending", 0, null, null, false, false), await Queues.RowAsync(postgres, _jobs, id));
        Assert.Equal(1, Assert.Single(await Store.ClaimAsync(_jobs, "worker", [type], 10, _lease, Ct)).Item.Attempt);
    }

    [Fact]
    public async Task A_long_error_is_truncated_to_fit_the_column()
    {
        var type = Queues.UniqueKey("test.long-error");
        var id = await Queues.ScheduleAsync(postgres, type);
        Assert.Single(await Store.ClaimAsync(_jobs, "worker", [type], 10, _lease, Ct));

        await Store.FailAsync(_jobs, id, "worker", new string('x', 5000), TimeSpan.FromMinutes(1), Ct);

        Assert.Equal(QueueColumns.MaxErrorLength, (await Queues.RowAsync(postgres, _jobs, id)).LastError!.Length);
    }

    [Fact]
    public async Task Claims_carry_the_type_payload_and_correlation_ID()
    {
        var source = Queues.UniqueKey("test.claim-inbox");
        const string rawBody = """{"b":1,  "a":[2,3]}""";
        await using (var db = PostgresFixture.CreateContext(postgres.AppConnectionString))
        {
            Assert.True(await new EfInbox(db, Queues.Settings()).AcceptAsync(source, "delivery-1", "InvoiceSettled", rawBody, "corr-1", Ct));
        }

        var claimed = Assert.Single(await Store.ClaimAsync(QueueDefinition.Inbox, "worker", [source], 10, _lease, Ct));

        Assert.Equal(source, claimed.HandlerKey);
        Assert.Equal(("InvoiceSettled", rawBody, 1, "corr-1"), (claimed.Item.Type, claimed.Item.Payload, claimed.Item.Attempt, claimed.Item.CorrelationId));
    }
}
