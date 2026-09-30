using Freeside.Core.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Freeside.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IJobScheduler"/>, <see cref="IInbox"/> and <see cref="IOutbox"/>.
    /// Requires <c>AddFreesideDatabase</c>.
    /// </summary>
    public static IServiceCollection AddFreesideMessaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<WorkQueueOptions>()
            .BindConfiguration(WorkQueueOptions.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<WorkQueueOptions>, WorkQueueOptionsValidator>());
        services.TryAddScoped<IJobScheduler, EfJobScheduler>();
        services.TryAddScoped<IInbox, EfInbox>();
        services.TryAddScoped<IOutbox, EfOutbox>();
        return services;
    }

    /// <summary>
    /// Adds the job runner, inbox processor and outbox dispatcher as hosted services (the worker
    /// only). Each queue is polled only if handlers for it are registered.
    /// </summary>
    public static IServiceCollection AddFreesideQueueProcessors(this IServiceCollection services)
    {
        services.AddFreesideMessaging();
        services.TryAddSingleton<WorkQueueStore>();
        services.AddHostedService<JobRunner>();
        services.AddHostedService<InboxProcessor>();
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
}
