using Microsoft.Extensions.DependencyInjection;

namespace Freeside.Core.Fakes;

public static class FakeServiceCollectionExtensions
{
    /// <summary>
    /// Registers a fake as a singleton. Fakes must be registered by type (not by factory) so that
    /// <see cref="FakeServiceGuard"/> can see them.
    /// </summary>
    public static IServiceCollection AddFake<TService, TFake>(this IServiceCollection services)
        where TService : class
        where TFake : class, TService, IFakeService
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<TService, TFake>();
    }
}
