using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Freeside.Infrastructure;

/// <summary>
/// For <c>dotnet ef migrations add</c> and <c>has-pending-model-changes</c>, which need the model
/// but no database. To apply migrations, pass a real connection with <c>--connection</c>.
/// </summary>
public sealed class FreesideDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FreesideDbContext>
{
    public FreesideDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<FreesideDbContext>();
        FreesideDbContextOptions.Configure(builder, "Host=localhost;Database=freeside;Username=freeside_design");
        return new FreesideDbContext(builder.Options);
    }
}
