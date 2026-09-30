namespace Freeside.Core.Messaging;

/// <summary>Schedules background jobs, which the worker's job runner executes.</summary>
public interface IJobScheduler
{
    /// <summary>
    /// Adds a job that runs at or after <paramref name="runAfter"/> (now if null). With a
    /// <paramref name="dedupeKey"/>, a second job with the same key is not added and this returns
    /// null; otherwise it returns the new job's ID. The job is stored before this returns.
    /// </summary>
    Task<long?> ScheduleAsync(
        string jobType,
        string payloadJson,
        DateTimeOffset? runAfter = null,
        string? dedupeKey = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
