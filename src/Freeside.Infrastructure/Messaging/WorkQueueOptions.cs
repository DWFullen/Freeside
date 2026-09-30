using Microsoft.Extensions.Options;

namespace Freeside.Infrastructure.Messaging;

/// <summary>The <c>WorkQueues</c> configuration section, shared by the job, inbox and outbox queues.</summary>
public sealed class WorkQueueOptions
{
    public const string SectionName = "WorkQueues";

    /// <summary>How long a worker waits before polling again after finding less than a full batch.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 10;

    /// <summary>How long a claim lasts. A handler must finish within it, or another worker may run the item too.</summary>
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Attempts before an item goes to Dead. Applies to items created after a change.</summary>
    public int MaxAttempts { get; set; } = 10;

    public TimeSpan BackoffBase { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan BackoffCap { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Polling loops per queue in each worker process.</summary>
    public int Concurrency { get; set; } = 1;
}

internal sealed class WorkQueueOptionsValidator : IValidateOptions<WorkQueueOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkQueueOptions options)
    {
        var failures = new List<string>();
        if (options.PollInterval <= TimeSpan.Zero || options.PollInterval > TimeSpan.FromMinutes(1))
        {
            failures.Add("WorkQueues:PollInterval must be more than 0 and at most 1 minute.");
        }

        if (options.BatchSize is < 1 or > 100)
        {
            failures.Add("WorkQueues:BatchSize must be between 1 and 100.");
        }

        if (options.Lease <= TimeSpan.Zero)
        {
            failures.Add("WorkQueues:Lease must be more than 0.");
        }

        if (options.MaxAttempts is < 1 or > 100)
        {
            failures.Add("WorkQueues:MaxAttempts must be between 1 and 100.");
        }

        if (options.BackoffBase <= TimeSpan.Zero || options.BackoffCap < options.BackoffBase)
        {
            failures.Add("WorkQueues:BackoffBase must be more than 0 and no more than BackoffCap.");
        }

        if (options.Concurrency is < 1 or > 32)
        {
            failures.Add("WorkQueues:Concurrency must be between 1 and 32.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
