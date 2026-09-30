using Freeside.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Freeside.Infrastructure.Tests.Messaging;

internal sealed record QueueRow(string Status, int Attempts, string? LockedBy, string? LastError, bool RunAfterIsInTheFuture, bool IsCompleted);

/// <summary>
/// Helpers for the queue tests. Each test uses its own job types, sources or message types, and
/// claims only those, so tests sharing the database never see each other's rows.
/// </summary>
internal static class Queues
{
    public static string UniqueKey(string prefix) => $"{prefix}.{Guid.NewGuid():N}";

    public static IOptions<WorkQueueOptions> Settings(int maxAttempts = 10) =>
        Options.Create(new WorkQueueOptions { MaxAttempts = maxAttempts });

    public static async Task<long> ScheduleAsync(
        PostgresFixture postgres, string jobType, int maxAttempts = 10, DateTimeOffset? runAfter = null, string payloadJson = "{}")
    {
        await using var db = PostgresFixture.CreateContext(postgres.AppConnectionString);
        var id = await new EfJobScheduler(db, Settings(maxAttempts))
            .ScheduleAsync(jobType, payloadJson, runAfter, cancellationToken: TestContext.Current.CancellationToken);
        return id!.Value;
    }

    public static async Task<QueueRow> RowAsync(PostgresFixture postgres, QueueDefinition queue, long id)
    {
        await using var connection = new NpgsqlConnection(postgres.SuperuserConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand($"""
            SELECT status, attempts, locked_by, last_error, run_after > now(), completed_at IS NOT NULL
            FROM {queue.Table} WHERE id = @id
            """, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), $"No row {id} in {queue.Table}.");
        return new QueueRow(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetBoolean(5));
    }

    public static Task<long> CountAsync(PostgresFixture postgres, string sqlWhere, params (string Name, object Value)[] parameters) =>
        ScalarAsync<long>(postgres, $"SELECT count(*) FROM {sqlWhere}", parameters);

    public static async Task<T> ScalarAsync<T>(PostgresFixture postgres, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(postgres.SuperuserConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>Stands in for time passing: the row's lease has run out.</summary>
    public static Task ExpireLeaseAsync(PostgresFixture postgres, QueueDefinition queue, long id) =>
        PostgresFixture.ExecuteAsync(postgres.SuperuserConnectionString,
            $"UPDATE {queue.Table} SET locked_until = now() - interval '1 second' WHERE id = @id", ("id", id));

    /// <summary>Stands in for time passing: the row's backoff has elapsed.</summary>
    public static Task MakeDueAsync(PostgresFixture postgres, QueueDefinition queue, long id) =>
        PostgresFixture.ExecuteAsync(postgres.SuperuserConnectionString,
            $"UPDATE {queue.Table} SET run_after = now() WHERE id = @id", ("id", id));

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, string what)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, $"Timed out waiting until {what}.");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
