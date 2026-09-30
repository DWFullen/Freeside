using Freeside.Core.Payments;

namespace Freeside.Core.Tests.Payments;

public sealed class RailHealthTests
{
    private readonly ManualClock _clock = new();
    private readonly OptionsBox<PaymentOptions> _options = new(new PaymentOptions());
    private readonly RailHealth _health;

    public RailHealthTests() => _health = new RailHealth(_clock, _options);

    [Fact]
    public void Three_failures_in_a_row_open_the_breaker_and_it_half_opens_after_the_cooldown()
    {
        _health.RecordFailure(RailId.Strike, RailFailureKind.Transient);
        _health.RecordFailure(RailId.Strike, RailFailureKind.Transient);
        Assert.Equal(BreakerState.Closed, _health.GetState(RailId.Strike));

        _health.RecordFailure(RailId.Strike, RailFailureKind.Transient);
        Assert.Equal(BreakerState.Open, _health.GetState(RailId.Strike));
        Assert.False(_health.IsAvailable(RailId.Strike));

        _clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(BreakerState.Open, _health.GetState(RailId.Strike));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(BreakerState.HalfOpen, _health.GetState(RailId.Strike));
        Assert.True(_health.IsAvailable(RailId.Strike));
    }

    [Fact]
    public void A_success_resets_the_failure_count()
    {
        _health.RecordFailure(RailId.Strike, RailFailureKind.Transient);
        _health.RecordFailure(RailId.Strike, RailFailureKind.Transient);
        _health.RecordSuccess(RailId.Strike);
        _health.RecordFailure(RailId.Strike, RailFailureKind.Transient);
        _health.RecordFailure(RailId.Strike, RailFailureKind.Transient);

        Assert.Equal(BreakerState.Closed, _health.GetState(RailId.Strike));
    }

    [Fact]
    public void An_authentication_failure_opens_the_breaker_at_once()
    {
        _health.RecordFailure(RailId.Strike, RailFailureKind.Authentication);

        Assert.Equal(BreakerState.Open, _health.GetState(RailId.Strike));
    }

    [Fact]
    public void Half_open_closes_after_three_successes_and_reopens_on_a_failure()
    {
        _health.RecordFailure(RailId.Strike, RailFailureKind.Authentication);
        _clock.Advance(TimeSpan.FromMinutes(1));

        _health.RecordSuccess(RailId.Strike);
        _health.RecordFailure(RailId.Strike, RailFailureKind.Transient);
        Assert.Equal(BreakerState.Open, _health.GetState(RailId.Strike));

        _clock.Advance(TimeSpan.FromMinutes(1));
        _health.RecordSuccess(RailId.Strike);
        _health.RecordSuccess(RailId.Strike);
        Assert.Equal(BreakerState.HalfOpen, _health.GetState(RailId.Strike));
        _health.RecordSuccess(RailId.Strike);
        Assert.Equal(BreakerState.Closed, _health.GetState(RailId.Strike));
    }

    [Fact]
    public void Breakers_are_per_rail()
    {
        _health.RecordFailure(RailId.Strike, RailFailureKind.Authentication);

        Assert.Equal(BreakerState.Closed, _health.GetState(RailId.BtcpayOnchain));
    }

    [Fact]
    public void The_kill_switch_opens_the_rail_and_takes_effect_without_a_restart()
    {
        _options.CurrentValue.Rails["strike"] = new RailOptions { Disabled = true };
        Assert.Equal(BreakerState.Open, _health.GetState(RailId.Strike));

        _options.CurrentValue.Rails["strike"].Disabled = false;
        Assert.Equal(BreakerState.Closed, _health.GetState(RailId.Strike));
    }

    [Fact]
    public void A_held_rail_stays_open_through_the_cooldown_until_released()
    {
        _health.Hold(RailId.BtcpayOnchain, "network not checked yet");
        _clock.Advance(TimeSpan.FromHours(1));
        _health.RecordSuccess(RailId.BtcpayOnchain);

        Assert.Equal(BreakerState.Open, _health.GetState(RailId.BtcpayOnchain));
        Assert.Equal("network not checked yet", _health.HeldBecause(RailId.BtcpayOnchain));

        _health.Release(RailId.BtcpayOnchain);
        Assert.Equal(BreakerState.Closed, _health.GetState(RailId.BtcpayOnchain));
        Assert.Null(_health.HeldBecause(RailId.BtcpayOnchain));
    }

    [Fact]
    public void An_account_rejection_applies_to_one_developer_and_expires()
    {
        _health.RejectAccount("dev-1", RailId.Strike);

        Assert.True(_health.IsRejected("dev-1", RailId.Strike));
        Assert.False(_health.IsRejected("dev-2", RailId.Strike));
        Assert.False(_health.IsRejected("dev-1", RailId.LnurlVerify));
        Assert.True(_health.IsAvailable(RailId.Strike));

        _clock.Advance(TimeSpan.FromHours(1));
        Assert.False(_health.IsRejected("dev-1", RailId.Strike));
    }
}
