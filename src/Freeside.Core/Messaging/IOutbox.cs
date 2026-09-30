namespace Freeside.Core.Messaging;

/// <summary>
/// Outgoing messages (emails, calls to other services) recorded in the same transaction as the
/// change that causes them, so they are sent if and only if that change commits.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Adds a message to the current unit of work. It is written with the scope's other changes, for
    /// example inside <see cref="Persistence.IUnitOfWork.ExecuteAsync"/>, and discarded if they roll back.
    /// </summary>
    void Add(string messageType, string payloadJson, string? correlationId = null);
}
