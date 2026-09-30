namespace Freeside.Core.Persistence;

/// <summary>
/// One database transaction around a piece of work, so that everything it writes (ledger entries,
/// outbox messages, state changes) commits together or not at all (project.md §4.5, step 4).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="work"/> in a transaction, saves what it added, and commits. If
    /// <paramref name="work"/> throws, nothing is written and the exception propagates.
    /// </summary>
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
}
