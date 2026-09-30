namespace Freeside.Infrastructure.Tests;

public sealed class ImagePinTests
{
    [Fact]
    public void Tests_use_the_same_postgres_image_as_the_regtest_stack()
    {
        var compose = File.ReadAllLines(Path.Combine(RepoRoot(), "tools", "regtest", "compose.yml"));
        var service = Array.FindIndex(compose, line => line.TrimEnd() == "  app-postgres:");
        Assert.True(service >= 0, "app-postgres service not found in tools/regtest/compose.yml");

        var image = compose.Skip(service + 1)
            .Select(line => line.Trim())
            .First(line => line.StartsWith("image:", StringComparison.Ordinal))["image:".Length..].Trim();

        Assert.Equal(PostgresFixture.Image, image);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Freeside.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Freeside.slnx not found above " + AppContext.BaseDirectory);
    }
}
