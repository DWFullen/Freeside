namespace Freeside.Infrastructure.Messaging;

/// <summary>
/// Where a queue lives. <see cref="HandlerKeyColumn"/> is what handlers register for; a worker only
/// claims rows whose key it has a handler for. All values are constants, never user input.
/// </summary>
internal sealed record QueueDefinition(string Name, string Table, string HandlerKeyColumn, string TypeColumn, bool PayloadIsJson)
{
    public static QueueDefinition Jobs { get; } = new("jobs", "jobs", "job_type", "job_type", PayloadIsJson: true);

    public static QueueDefinition Inbox { get; } = new("inbox", "inbox_messages", "source", "message_type", PayloadIsJson: false);

    public static QueueDefinition Outbox { get; } = new("outbox", "outbox_messages", "message_type", "message_type", PayloadIsJson: true);
}
