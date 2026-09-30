using Freeside.Core.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Freeside.Infrastructure;

public sealed class FreesideDbContext(DbContextOptions<FreesideDbContext> options) : DbContext(options)
{
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FreesideDbContext).Assembly);
}
