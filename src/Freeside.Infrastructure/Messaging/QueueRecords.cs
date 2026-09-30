namespace Freeside.Infrastructure.Messaging;

internal enum WorkStatus
{
    Pending = 1,
    Running = 2,
    Succeeded = 3,
    Dead = 4,
}

/// <summary>The columns every queue table shares; see <see cref="WorkQueueStore"/> for how they're used.</summary>
internal abstract class QueueRecord
{
    public long Id { get; set; }

    public WorkStatus Status { get; set; }

    public int Attempts { get; set; }

    public int MaxAttempts { get; set; }

    public DateTimeOffset RunAfter { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public string? LockedBy { get; set; }

    public string? LastError { get; set; }

    public string? CorrelationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

internal sealed class JobRecord : QueueRecord
{
    public string JobType { get; set; } = "";

    public string Payload { get; set; } = "";

    public string? DedupeKey { get; set; }
}

internal sealed class InboxRecord : QueueRecord
{
    public string Source { get; set; } = "";

    public string DedupeKey { get; set; } = "";

    public string MessageType { get; set; } = "";

    public string Payload { get; set; } = "";
}

internal sealed class OutboxRecord : QueueRecord
{
    public string MessageType { get; set; } = "";

    public string Payload { get; set; } = "";
}
