using Microsoft.Extensions.Options;

namespace Freeside.Core.Payments;

public enum BreakerState
{
    /// <summary>Healthy: requests go through.</summary>
    Closed = 1,

    /// <summary>Off: checkout fails over to the developer's next rail (project.md §4.2, P8).</summary>
    Open = 2,

    /// <summary>The cooldown has passed: requests go through as trials, and enough successes close it.</summary>
    HalfOpen = 3,
}

/// <summary>
/// Circuit breakers for each rail, platform-wide, and rails turned off for one developer
/// (project.md §4.2, failover). Held in memory, so each process keeps its own state.
/// </summary>
public sealed class RailHealth(TimeProvider time, IOptionsMonitor<PaymentOptions> options)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<RailId, Breaker> _breakers = [];
    private readonly Dictionary<(string DeveloperId, RailId Rail), DateTimeOffset> _rejections = [];

    public BreakerState GetState(RailId rail)
    {
        if (options.CurrentValue.Rails.TryGetValue(rail.Value, out var settings) && settings.Disabled)
        {
            return BreakerState.Open;
        }

        lock (_lock)
        {
            return StateOf(rail);
        }
    }

    public bool IsAvailable(RailId rail) => GetState(rail) != BreakerState.Open;

    /// <summary>Why the rail is held open, if it is (see <see cref="Hold"/>).</summary>
    public string? HeldBecause(RailId rail)
    {
        lock (_lock)
        {
            return _breakers.TryGetValue(rail, out var breaker) ? breaker.HeldBecause : null;
        }
    }

    public void RecordSuccess(RailId rail)
    {
        lock (_lock)
        {
            var breaker = Get(rail);
            switch (StateOf(rail))
            {
                case BreakerState.Closed:
                    breaker.Failures = 0;
                    break;
                case BreakerState.HalfOpen when ++breaker.Successes >= options.CurrentValue.Breaker.SuccessThreshold:
                    breaker.OpenedAt = null;
                    breaker.Failures = 0;
                    breaker.Successes = 0;
                    break;
            }
        }
    }

    public void RecordFailure(RailId rail, RailFailureKind kind)
    {
        lock (_lock)
        {
            var breaker = Get(rail);
            breaker.Failures++;
            if (kind == RailFailureKind.Authentication
                || StateOf(rail) == BreakerState.HalfOpen
                || breaker.Failures >= options.CurrentValue.Breaker.FailureThreshold)
            {
                breaker.OpenedAt = time.GetUtcNow();
                breaker.Successes = 0;
            }
        }
    }

    /// <summary>
    /// Keeps the rail open until <see cref="Release"/>, whatever else happens: for a rail that isn't
    /// configured, or whose network hasn't been checked yet.
    /// </summary>
    public void Hold(RailId rail, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_lock)
        {
            Get(rail).HeldBecause = reason;
        }
    }

    public void Release(RailId rail)
    {
        lock (_lock)
        {
            Get(rail).HeldBecause = null;
        }
    }

    /// <summary>Turns the rail off for one developer for a while, for example when it rejects their account.</summary>
    public void RejectAccount(string developerId, RailId rail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(developerId);
        lock (_lock)
        {
            _rejections[(developerId, rail)] = time.GetUtcNow() + options.CurrentValue.Breaker.AccountRejectionTtl;
        }
    }

    public bool IsRejected(string developerId, RailId rail)
    {
        lock (_lock)
        {
            if (!_rejections.TryGetValue((developerId, rail), out var until))
            {
                return false;
            }

            if (time.GetUtcNow() < until)
            {
                return true;
            }

            _rejections.Remove((developerId, rail));
            return false;
        }
    }

    private BreakerState StateOf(RailId rail)
    {
        if (!_breakers.TryGetValue(rail, out var breaker) || (breaker.OpenedAt is null && breaker.HeldBecause is null))
        {
            return BreakerState.Closed;
        }

        if (breaker.HeldBecause is not null)
        {
            return BreakerState.Open;
        }

        return time.GetUtcNow() - breaker.OpenedAt >= options.CurrentValue.Breaker.Cooldown
            ? BreakerState.HalfOpen
            : BreakerState.Open;
    }

    private Breaker Get(RailId rail)
    {
        if (!_breakers.TryGetValue(rail, out var breaker))
        {
            breaker = new Breaker();
            _breakers[rail] = breaker;
        }

        return breaker;
    }

    private sealed class Breaker
    {
        public int Failures { get; set; }

        public int Successes { get; set; }

        public DateTimeOffset? OpenedAt { get; set; }

        public string? HeldBecause { get; set; }
    }
}
