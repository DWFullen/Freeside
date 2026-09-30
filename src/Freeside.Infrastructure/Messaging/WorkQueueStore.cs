using Freeside.Core.Messaging;
using Npgsql;

namespace Freeside.Infrastructure.Messaging;

internal sealed record ClaimedItem(string HandlerKey, WorkItem Item);

internal enum FailureOutcome
{
    /// <summary>Back to Pending, to run again after the backoff.</summary>
    Retry,

    /// <summary>Out of attempts.</summary>
    Dead,

    /// <summary>This worker no longer holds the lease, so nothing changed.</summary>
    Fenced,
}

/// <summary>
/// Claims and settles queue rows with plain SQL. Claiming uses <c>FOR UPDATE SKIP LOCKED</c>, so
/// concurrent workers never claim the same row, and a lease: a row whose lease has expired (the
/// worker crashed or stalled) is claimed again, and that counts as an attempt. Every settle is
/// fenced on <c>locked_by</c>, so a worker that lost its lease can't overwrite the new holder's
/// result. Delivery is therefore at least once, never exactly once.
/// </summary>
internal sealed class WorkQueueStore(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<ClaimedItem>> ClaimAsync(
        QueueDefinition queue, string worker, IReadOnlyCollection<string> handlerKeys, int batchSize, TimeSpan lease, CancellationToken cancellationToken)
    {
        var payload = queue.PayloadIsJson ? "q.payload::text" : "q.payload";
        await using var command = dataSource.CreateCommand($"""
            WITH claimable AS (
                SELECT id FROM {queue.Table}
                WHERE {queue.HandlerKeyColumn} = ANY(@keys)
                  AND attempts < max_attempts
                  AND ((status = 'Pending' AND run_after <= now())
                    OR (status = 'Running' AND locked_until < now()))
                ORDER BY run_after, id
                LIMIT @batch
                FOR UPDATE SKIP LOCKED)
            UPDATE {queue.Table} AS q
            SET status = 'Running', locked_by = @worker, locked_until = now() + @lease, attempts = q.attempts + 1
            FROM claimable
            WHERE q.id = claimable.id
            RETURNING q.id, q.{queue.HandlerKeyColumn}, q.{queue.TypeColumn}, {payload}, q.attempts, q.correlation_id
            """);
        command.Parameters.AddWithValue("keys", handlerKeys.ToArray());
        command.Parameters.AddWithValue("batch", batchSize);
        command.Parameters.AddWithValue("worker", worker);
        command.Parameters.AddWithValue("lease", lease);

        var claimed = new List<ClaimedItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            claimed.Add(new ClaimedItem(
                reader.GetString(1),
                new WorkItem(
                    reader.GetInt64(0),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5))));
        }

        return [.. claimed.OrderBy(c => c.Item.Id)];
    }

    public async Task<bool> CompleteAsync(QueueDefinition queue, long id, string worker, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"""
            UPDATE {queue.Table}
            SET status = 'Succeeded', completed_at = now(), locked_by = NULL, locked_until = NULL, last_error = NULL
            WHERE id = @id AND status = 'Running' AND locked_by = @worker
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("worker", worker);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task<FailureOutcome> FailAsync(
        QueueDefinition queue, long id, string worker, string error, TimeSpan backoff, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"""
            UPDATE {queue.Table}
            SET status = CASE WHEN attempts >= max_attempts THEN 'Dead' ELSE 'Pending' END,
                completed_at = CASE WHEN attempts >= max_attempts THEN now() END,
                run_after = CASE WHEN attempts >= max_attempts THEN run_after ELSE now() + @backoff END,
                last_error = @error, locked_by = NULL, locked_until = NULL
            WHERE id = @id AND status = 'Running' AND locked_by = @worker
            RETURNING status
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("worker", worker);
        command.Parameters.AddWithValue("error", Truncate(error, QueueColumns.MaxErrorLength));
        command.Parameters.AddWithValue("backoff", backoff);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) switch
        {
            "Dead" => FailureOutcome.Dead,
            "Pending" => FailureOutcome.Retry,
            _ => FailureOutcome.Fenced,
        };
    }

    /// <summary>Gives a claimed row back without counting the attempt, for a graceful shutdown.</summary>
    public async Task<bool> ReleaseAsync(QueueDefinition queue, long id, string worker, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"""
            UPDATE {queue.Table}
            SET status = 'Pending', attempts = attempts - 1, locked_by = NULL, locked_until = NULL
            WHERE id = @id AND status = 'Running' AND locked_by = @worker
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("worker", worker);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    /// <summary>Rows whose lease expired on their last attempt go to Dead. Returns their IDs.</summary>
    public async Task<IReadOnlyList<long>> SweepAsync(QueueDefinition queue, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"""
            UPDATE {queue.Table}
            SET status = 'Dead', completed_at = now(), locked_by = NULL, locked_until = NULL,
                last_error = 'The lease expired on the last attempt: the worker crashed, stalled or was stopped.'
            WHERE status = 'Running' AND locked_until < now() AND attempts >= max_attempts
            RETURNING id
            """);
        var dead = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            dead.Add(reader.GetInt64(0));
        }

        return dead;
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
