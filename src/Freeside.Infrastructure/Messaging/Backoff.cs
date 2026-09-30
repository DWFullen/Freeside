namespace Freeside.Infrastructure.Messaging;

internal static class Backoff
{
    /// <summary>
    /// The delay before the next try after <paramref name="attempt"/> failed: base × 2^(attempt − 1)
    /// plus up to one base of random jitter, never more than <paramref name="cap"/>. Integer ticks
    /// throughout: binary floating point is banned in this code (AGENTS.md §2, invariant 5).
    /// </summary>
    public static TimeSpan After(int attempt, TimeSpan baseDelay, TimeSpan cap, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (baseDelay <= TimeSpan.Zero || cap < baseDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Need 0 < base ≤ cap.");
        }

        var delay = baseDelay.Ticks;
        for (var doubling = 1; doubling < attempt && delay < cap.Ticks; doubling++)
        {
            delay = delay > cap.Ticks / 2 ? cap.Ticks : delay * 2;
        }

        var jitter = random.NextInt64(0, baseDelay.Ticks + 1);
        return TimeSpan.FromTicks(delay > cap.Ticks - jitter ? cap.Ticks : delay + jitter);
    }
}
