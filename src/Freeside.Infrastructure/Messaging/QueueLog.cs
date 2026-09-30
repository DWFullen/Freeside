using Microsoft.Extensions.Logging;

namespace Freeside.Infrastructure.Messaging;

internal static partial class QueueLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "No handlers are registered for the {Queue} queue; not polling it.")]
    public static partial void NoHandlers(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Queue} item {Id} ({Type}) failed on attempt {Attempt}; retrying in {Backoff}.")]
    public static partial void WillRetry(ILogger logger, string queue, long id, string type, int attempt, TimeSpan backoff, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Queue} item {Id} ({Type}) failed on its last attempt ({Attempt}) and is now Dead. It needs review.")]
    public static partial void Dead(ILogger logger, string queue, long id, string type, int attempt, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Queue} item {Id}'s lease expired on its last attempt; it is now Dead. It needs review.")]
    public static partial void LeaseExpiredDead(ILogger logger, string queue, long id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Queue} item {Id}: this worker's lease was taken over before it finished, so its result was not recorded. The item may run twice; handlers are idempotent.")]
    public static partial void Fenced(ILogger logger, string queue, long id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Polling the {Queue} queue failed; trying again after the poll interval.")]
    public static partial void PollFailed(ILogger logger, string queue, Exception exception);
}
