using Freeside.Core.Bitcoin;
using Freeside.Core.Payments;
using Freeside.Infrastructure.Database;
using Freeside.Infrastructure.Messaging;

namespace Freeside.Worker;

/// <summary>
/// The worker's composition root, shared by <c>Program</c> and the tests.
/// </summary>
public static class WorkerServices
{
    public static IServiceCollection Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddBitcoinNetwork();
        services.AddFreesidePayments(configuration);
        services.AddFreesideFees(configuration);
        services.AddFreesideDatabase();
        services.AddFreesideQueueProcessors();
        services.AddHostedService<StartupLogger>();
        return services;
    }
}
