using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Freeside.Infrastructure;

/// <summary>One place for provider settings, so the runtime and design-time models match.</summary>
internal static class FreesideDbContextOptions
{
    public static void Configure(DbContextOptionsBuilder builder, NpgsqlDataSource dataSource) =>
        builder.UseNpgsql(dataSource).UseSnakeCaseNamingConvention();

    public static void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
