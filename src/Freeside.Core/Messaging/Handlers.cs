namespace Freeside.Core.Messaging;

/// <summary>Runs jobs of one <see cref="JobType"/>. Must be idempotent.</summary>
public interface IJobHandler
{
    string JobType { get; }

    Task HandleAsync(WorkItem job, CancellationToken cancellationToken);
}

/// <summary>Processes inbox messages from one <see cref="Source"/>, such as "btcpay". Must be idempotent.</summary>
public interface IInboxHandler
{
    string Source { get; }

    Task HandleAsync(WorkItem message, CancellationToken cancellationToken);
}

/// <summary>Delivers outbox messages of one <see cref="MessageType"/>. Must be idempotent.</summary>
public interface IOutboxHandler
{
    string MessageType { get; }

    Task HandleAsync(WorkItem message, CancellationToken cancellationToken);
}
