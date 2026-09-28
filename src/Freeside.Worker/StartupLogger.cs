using Freeside.Core.Bitcoin;
using Microsoft.Extensions.Options;

namespace Freeside.Worker;

/// <summary>
/// Logs the bound network once the host has started. The job runner replaces this as the worker's
/// real workload in Phase 0 PR 5.
/// </summary>
internal sealed partial class StartupLogger(IOptions<BitcoinOptions> options, ILogger<StartupLogger> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger, options.Value.Network);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker started on Bitcoin network {Network}")]
    private static partial void LogStarted(ILogger logger, string? network);
}
