using Freeside.Core.Persistence;

namespace Freeside.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(FreesideDbContext db) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("A unit of work is already running in this scope; units of work don't nest.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await work(cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Rolled back when the transaction is disposed. Drop anything the work added but didn't
            // save, such as outbox messages, so a later save in this scope can't write it.
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
