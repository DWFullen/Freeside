using Freeside.Core.Bitcoin;
using Freeside.Infrastructure.Database;
using Freeside.Infrastructure.Messaging;

namespace Freeside.Worker;

/// <summary>
/// The worker's composition root, shared by <c>Program</c> and the tests.
/// </summary>
public static class WorkerServices
{
    public static IServiceCollection Configure(IServiceCollection services)
    {
        services.AddBitcoinNetwork();
        services.AddFreesideDatabase();
        services.AddFreesideQueueProcessors();
        services.AddHostedService<StartupLogger>();
        return services;
    }
}
