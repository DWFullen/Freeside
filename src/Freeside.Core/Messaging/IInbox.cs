namespace Freeside.Core.Messaging;

/// <summary>
/// Incoming messages, such as verified webhooks. A receiver stores the message here and only
/// then acknowledges it (AGENTS.md §2, invariant 6); the worker processes it later.
/// </summary>
public interface IInbox
{
    /// <summary>
    /// Stores the message before returning. Returns false, and stores nothing, if a message with the
    /// same <paramref name="source"/> and <paramref name="dedupeKey"/> (for example BTCPay's
    /// <c>deliveryId</c>) was already accepted.
    /// </summary>
    Task<bool> AcceptAsync(
        string source,
        string dedupeKey,
        string messageType,
        string payload,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
