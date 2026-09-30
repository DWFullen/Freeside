using Freeside.Core.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Freeside.Infrastructure.Messaging;

/// <summary>
/// Polls one queue and hands each claimed item to its handler in a fresh DI scope. A failure goes
/// back to the queue with backoff; a database error is logged and retried, so an outage never
/// stops the worker. On shutdown, claimed items that haven't finished are released.
/// </summary>
internal abstract class QueueProcessor<THandler>(
    QueueDefinition queue,
    WorkQueueStore store,
    IServiceScopeFactory scopes,
    IOptions<WorkQueueOptions> options,
    ILogger logger) : BackgroundService
    where THandler : class
{
    protected abstract string KeyOf(THandler handler);

    protected abstract Task HandleAsync(THandler handler, WorkItem item, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var keys = HandlerKeys();
        if (keys.Count == 0)
        {
            QueueLog.NoHandlers(logger, queue.Name);
            return;
        }

        var settings = options.Value;
        var run = Guid.NewGuid().ToString("N")[..8];
        await Task.WhenAll(Enumerable.Range(0, settings.Concurrency)
            .Select(loop => PollAsync($"{Environment.MachineName}/{Environment.ProcessId}/{queue.Name}/{run}/{loop}", keys, settings, stoppingToken)))
            .ConfigureAwait(false);
    }

    private List<string> HandlerKeys()
    {
        using var scope = scopes.CreateScope();
        var keys = scope.ServiceProvider.GetServices<THandler>().Select(KeyOf).ToList();
        var duplicates = keys.GroupBy(k => k, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        return duplicates.Count == 0
            ? keys
            : throw new InvalidOperationException($"More than one {typeof(THandler).Name} is registered for: {string.Join(", ", duplicates)}.");
    }

    private async Task PollAsync(string worker, List<string> keys, WorkQueueOptions settings, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimedCount = 0;
            try
            {
                foreach (var id in await store.SweepAsync(queue, stoppingToken).ConfigureAwait(false))
                {
                    QueueLog.LeaseExpiredDead(logger, queue.Name, id);
                }

                // Not cancelled mid-query: a claim that commits after the reader is abandoned would
                // hold its rows until the lease expires, and cost each one an attempt.
                var claimed = await store.ClaimAsync(queue, worker, keys, settings.BatchSize, settings.Lease, CancellationToken.None).ConfigureAwait(false);
                claimedCount = claimed.Count;
                for (var i = 0; i < claimed.Count; i++)
                {
                    if (stoppingToken.IsCancellationRequested || !await ProcessAsync(worker, claimed[i], settings, stoppingToken).ConfigureAwait(false))
                    {
                        await ReleaseAsync(worker, claimed.Skip(i)).ConfigureAwait(false);
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                QueueLog.PollFailed(logger, queue.Name, ex);
                claimedCount = 0;
            }

            if (claimedCount < settings.BatchSize)
            {
                try
                {
                    await Task.Delay(settings.PollInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    /// <summary>Returns false if the worker is stopping and the item was not finished.</summary>
    private async Task<bool> ProcessAsync(string worker, ClaimedItem claimed, WorkQueueOptions settings, CancellationToken stoppingToken)
    {
        var item = claimed.Item;
        using var logScope = logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = item.CorrelationId, ["WorkItemId"] = item.Id });
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<THandler>().First(h => KeyOf(h) == claimed.HandlerKey);

        try
        {
            await HandleAsync(handler, item, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
#pragma warning disable CA1031 // Any handler failure is recorded on the item and retried; it must not stop the worker.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            var backoff = Backoff.After(item.Attempt, settings.BackoffBase, settings.BackoffCap, Random.Shared);
            var outcome = await store.FailAsync(queue, item.Id, worker, $"{ex.GetType().FullName}: {ex.Message}", backoff, CancellationToken.None).ConfigureAwait(false);
            switch (outcome)
            {
                case FailureOutcome.Dead:
                    QueueLog.Dead(logger, queue.Name, item.Id, item.Type, item.Attempt, ex);
                    break;
                case FailureOutcome.Retry:
                    QueueLog.WillRetry(logger, queue.Name, item.Id, item.Type, item.Attempt, backoff, ex);
                    break;
                default:
                    QueueLog.Fenced(logger, queue.Name, item.Id);
                    break;
            }

            return true;
        }

        if (!await store.CompleteAsync(queue, item.Id, worker, CancellationToken.None).ConfigureAwait(false))
        {
            QueueLog.Fenced(logger, queue.Name, item.Id);
        }

        return true;
    }

    private async Task ReleaseAsync(string worker, IEnumerable<ClaimedItem> unfinished)
    {
        foreach (var claimed in unfinished)
        {
            await store.ReleaseAsync(queue, claimed.Item.Id, worker, CancellationToken.None).ConfigureAwait(false);
        }
    }
}

internal sealed class JobRunner(WorkQueueStore store, IServiceScopeFactory scopes, IOptions<WorkQueueOptions> options, ILogger<JobRunner> logger)
    : QueueProcessor<IJobHandler>(QueueDefinition.Jobs, store, scopes, options, logger)
{
    protected override string KeyOf(IJobHandler handler) => handler.JobType;

    protected override Task HandleAsync(IJobHandler handler, WorkItem item, CancellationToken cancellationToken) =>
        handler.HandleAsync(item, cancellationToken);
}

internal sealed class InboxProcessor(WorkQueueStore store, IServiceScopeFactory scopes, IOptions<WorkQueueOptions> options, ILogger<InboxProcessor> logger)
    : QueueProcessor<IInboxHandler>(QueueDefinition.Inbox, store, scopes, options, logger)
{
    protected override string KeyOf(IInboxHandler handler) => handler.Source;

    protected override Task HandleAsync(IInboxHandler handler, WorkItem item, CancellationToken cancellationToken) =>
        handler.HandleAsync(item, cancellationToken);
}

internal sealed class OutboxDispatcher(WorkQueueStore store, IServiceScopeFactory scopes, IOptions<WorkQueueOptions> options, ILogger<OutboxDispatcher> logger)
    : QueueProcessor<IOutboxHandler>(QueueDefinition.Outbox, store, scopes, options, logger)
{
    protected override string KeyOf(IOutboxHandler handler) => handler.MessageType;

    protected override Task HandleAsync(IOutboxHandler handler, WorkItem item, CancellationToken cancellationToken) =>
        handler.HandleAsync(item, cancellationToken);
}
