using Freeside.Infrastructure.Messaging;

namespace Freeside.Infrastructure.Tests.Messaging;

public sealed class WorkQueueOptionsTests
{
    [Fact]
    public void The_defaults_are_valid() =>
        Assert.True(new WorkQueueOptionsValidator().Validate(null, new WorkQueueOptions()).Succeeded);

    public static TheoryData<string, Action<WorkQueueOptions>> Invalid => new()
    {
        { "PollInterval", o => o.PollInterval = TimeSpan.Zero },
        { "PollInterval", o => o.PollInterval = TimeSpan.FromMinutes(2) },
        { "BatchSize", o => o.BatchSize = 0 },
        { "BatchSize", o => o.BatchSize = 101 },
        { "Lease", o => o.Lease = TimeSpan.Zero },
        { "MaxAttempts", o => o.MaxAttempts = 0 },
        { "MaxAttempts", o => o.MaxAttempts = 101 },
        { "BackoffBase", o => o.BackoffBase = TimeSpan.Zero },
        { "BackoffBase", o => o.BackoffCap = TimeSpan.FromSeconds(1) },
        { "Concurrency", o => o.Concurrency = 0 },
        { "Concurrency", o => o.Concurrency = 33 },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Out_of_range_settings_are_refused(string setting, Action<WorkQueueOptions> change)
    {
        var options = new WorkQueueOptions();
        change(options);

        var result = new WorkQueueOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"WorkQueues:{setting}", result.FailureMessage, StringComparison.Ordinal);
    }
}
