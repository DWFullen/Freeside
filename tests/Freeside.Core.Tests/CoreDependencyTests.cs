using Freeside.Core.Bitcoin;

namespace Freeside.Core.Tests;

public sealed class CoreDependencyTests
{
    // Core holds domain code only. Infrastructure (EF Core, HTTP clients, Azure SDKs, ASP.NET Core)
    // lives in other projects behind Core interfaces.
    private static readonly HashSet<string> _allowedAssemblies =
    [
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.Configuration.Binder",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Options.ConfigurationExtensions",
        "Microsoft.Extensions.Primitives",
    ];

    [Fact]
    public void Core_references_only_the_runtime_and_allowed_abstractions()
    {
        var disallowed = typeof(BitcoinNetwork).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name != "netstandard"
                && !name.StartsWith("System", StringComparison.Ordinal)
                && !_allowedAssemblies.Contains(name))
            .ToList();

        Assert.Empty(disallowed);
    }
}
