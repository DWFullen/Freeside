using Freeside.Core.Ledger;
using Freeside.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Freeside.Infrastructure;

public sealed class FreesideDbContext(DbContextOptions<FreesideDbContext> options) : DbContext(options)
{
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    internal DbSet<JobRecord> Jobs => Set<JobRecord>();

    internal DbSet<InboxRecord> Inbox => Set<InboxRecord>();

    internal DbSet<OutboxRecord> Outbox => Set<OutboxRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FreesideDbContext).Assembly);
}
