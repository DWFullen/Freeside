using Freeside.Core.Ledger;
using Freeside.Core.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Freeside.Infrastructure.Messaging;

internal sealed class EfJobScheduler(FreesideDbContext db, IOptions<WorkQueueOptions> options) : IJobScheduler
{
    public async Task<long?> ScheduleAsync(
        string jobType,
        string payloadJson,
        DateTimeOffset? runAfter = null,
        string? dedupeKey = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        Require.Text(jobType, nameof(jobType), QueueColumns.MaxTypeLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        Require.OptionalText(dedupeKey, nameof(dedupeKey), QueueColumns.MaxDedupeKeyLength);
        Require.OptionalText(correlationId, nameof(correlationId), LedgerEntry.MaxIdLength);
        if (runAfter is { Offset.Ticks: not 0 })
        {
            throw new ArgumentException("runAfter must be UTC.", nameof(runAfter));
        }

        var ids = await db.Database.SqlQuery<long>($"""
            INSERT INTO jobs (job_type, payload, run_after, dedupe_key, correlation_id, max_attempts)
            VALUES ({jobType}, {payloadJson}::jsonb, COALESCE({runAfter}, now()), {dedupeKey}, {correlationId}, {options.Value.MaxAttempts})
            ON CONFLICT (dedupe_key) DO NOTHING
            RETURNING id AS "Value"
            """).ToListAsync(cancellationToken).ConfigureAwait(false);
        return ids.Count == 0 ? null : ids[0];
    }
}

internal sealed class EfInbox(FreesideDbContext db, IOptions<WorkQueueOptions> options) : IInbox
{
    /// <summary>Webhook bodies are small; anything larger is refused rather than stored.</summary>
    public const int MaxPayloadLength = 1_000_000;

    public async Task<bool> AcceptAsync(
        string source,
        string dedupeKey,
        string messageType,
        string payload,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        Require.Text(source, nameof(source), LedgerEntry.MaxNameLength);
        Require.Text(dedupeKey, nameof(dedupeKey), QueueColumns.MaxDedupeKeyLength);
        Require.Text(messageType, nameof(messageType), QueueColumns.MaxTypeLength);
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length > MaxPayloadLength)
        {
            throw new ArgumentException($"The payload is longer than {MaxPayloadLength} characters.", nameof(payload));
        }

        Require.OptionalText(correlationId, nameof(correlationId), LedgerEntry.MaxIdLength);

        var ids = await db.Database.SqlQuery<long>($"""
            INSERT INTO inbox_messages (source, dedupe_key, message_type, payload, correlation_id, max_attempts)
            VALUES ({source}, {dedupeKey}, {messageType}, {payload}, {correlationId}, {options.Value.MaxAttempts})
            ON CONFLICT (source, dedupe_key) DO NOTHING
            RETURNING id AS "Value"
            """).ToListAsync(cancellationToken).ConfigureAwait(false);
        return ids.Count == 1;
    }
}

internal sealed class EfOutbox(FreesideDbContext db, IOptions<WorkQueueOptions> options) : IOutbox
{
    public void Add(string messageType, string payloadJson, string? correlationId = null)
    {
        Require.Text(messageType, nameof(messageType), QueueColumns.MaxTypeLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        Require.OptionalText(correlationId, nameof(correlationId), LedgerEntry.MaxIdLength);

        db.Outbox.Add(new OutboxRecord
        {
            MessageType = messageType,
            Payload = payloadJson,
            CorrelationId = correlationId,
            MaxAttempts = options.Value.MaxAttempts,
            Status = WorkStatus.Pending,
        });
    }
}

internal static class Require
{
    public static void Text(string? value, string name, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        OptionalText(value, name, maxLength);
    }

    public static void OptionalText(string? value, string name, int maxLength)
    {
        if (value is not null && (value.Length > maxLength || value.Trim().Length != value.Length || value.Length == 0))
        {
            throw new ArgumentException($"{name} must be 1 to {maxLength} characters with no leading or trailing whitespace.", name);
        }
    }
}
