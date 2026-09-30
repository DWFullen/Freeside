namespace Freeside.Core.Messaging;

/// <summary>
/// A job, inbox message or outbox message claimed for processing. Delivery is at least once: the
/// same item can reach a handler again after a crash or an expired lease, so handlers must be
/// idempotent (AGENTS.md §4.6).
/// </summary>
/// <param name="Id">The row's ID in its queue table.</param>
/// <param name="Type">The job type, or the message type.</param>
/// <param name="Payload">JSON for jobs and outbox messages; the verified raw body for inbox messages.</param>
/// <param name="Attempt">1 on the first try.</param>
/// <param name="CorrelationId">Carries through invoice, webhook, entitlement and download (AGENTS.md §7.5).</param>
public sealed record WorkItem(long Id, string Type, string Payload, int Attempt, string? CorrelationId);
