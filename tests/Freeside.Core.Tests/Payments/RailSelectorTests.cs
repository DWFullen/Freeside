using Freeside.Core.Monetary;
using Freeside.Core.Payments;

namespace Freeside.Core.Tests.Payments;

/// <summary>project.md §4.2 (priority and failover) and §4.3 (the $10 on-chain minimum).</summary>
public sealed class RailSelectorTests
{
    private static readonly RailId _lnurl = RailId.LnurlVerify;
    private static readonly Money _twentyDollars = new(2_000, Currency.Usd);

    private readonly ManualClock _clock = new();
    private readonly OptionsBox<PaymentOptions> _options = new(new PaymentOptions());
    private readonly RailHealth _health;
    private readonly RailSelector _selector;

    public RailSelectorTests()
    {
        _health = new RailHealth(_clock, _options);
        _selector = new RailSelector(
            [new StubRail(RailId.Strike, RailLayer.Lightning), new StubRail(_lnurl, RailLayer.Lightning), new StubRail(RailId.BtcpayOnchain, RailLayer.OnChain)],
            _health,
            _options);
    }

    private static DeveloperRailPlan Plan(string developerId = "dev-1") => new(
        developerId,
        [new RailChoice(RailId.Strike, "handle"), new RailChoice(RailId.BtcpayOnchain, "store-1")],
        [new RailChoice(RailId.Strike, "handle"), new RailChoice(_lnurl, "dev@example.test")]);

    [Fact]
    public void Picks_the_first_rail_of_each_layer_in_the_developers_order()
    {
        var selection = _selector.Select(Plan(), _twentyDollars);

        // Strike is Lightning, so it's skipped for the on-chain layer.
        Assert.Equal((RailId.BtcpayOnchain, "store-1"), (selection.OnChain!.Rail.Id, selection.OnChain.Account));
        Assert.Equal((RailId.Strike, "handle"), (selection.Lightning!.Rail.Id, selection.Lightning.Account));
    }

    [Fact]
    public void An_open_breaker_fails_over_to_the_next_rail()
    {
        _health.RecordFailure(RailId.Strike, RailFailureKind.Authentication);

        Assert.Equal(_lnurl, _selector.Select(Plan(), _twentyDollars).Lightning!.Rail.Id);
    }

    [Fact]
    public void A_rejected_account_fails_over_for_that_developer_only()
    {
        _health.RejectAccount("dev-1", RailId.Strike);

        Assert.Equal(_lnurl, _selector.Select(Plan("dev-1"), _twentyDollars).Lightning!.Rail.Id);
        Assert.Equal(RailId.Strike, _selector.Select(Plan("dev-2"), _twentyDollars).Lightning!.Rail.Id);
    }

    [Fact]
    public void The_kill_switch_fails_over_every_developer()
    {
        _options.CurrentValue.Rails["strike"] = new RailOptions { Disabled = true };

        Assert.Equal(_lnurl, _selector.Select(Plan(), _twentyDollars).Lightning!.Rail.Id);
    }

    [Theory]
    [InlineData(999, false)]
    [InlineData(1_000, true)]
    public void On_chain_is_offered_from_ten_dollars(long cents, bool offered)
    {
        var selection = _selector.Select(Plan(), new Money(cents, Currency.Usd));

        Assert.Equal(offered, selection.OnChain is not null);
        Assert.NotNull(selection.Lightning);
    }

    [Fact]
    public void Unregistered_rails_are_skipped_and_nothing_healthy_means_no_selection()
    {
        _health.RecordFailure(RailId.Strike, RailFailureKind.Authentication);
        _health.RecordFailure(_lnurl, RailFailureKind.Authentication);
        _health.Hold(RailId.BtcpayOnchain, "not configured");
        var plan = Plan() with { Lightning = [new RailChoice(new RailId("nwc"), "x"), .. Plan().Lightning] };

        var selection = _selector.Select(plan, _twentyDollars);

        Assert.False(selection.Any);
    }

    [Fact]
    public void Order_values_must_be_positive_USD()
    {
        Assert.Throws<ArgumentException>(() => _selector.Select(Plan(), new Money(2_000, Currency.FromCode("EUR"))));
        Assert.Throws<ArgumentOutOfRangeException>(() => _selector.Select(Plan(), new Money(0, Currency.Usd)));
    }

    [Fact]
    public void Two_rails_with_the_same_ID_are_refused() =>
        Assert.Throws<InvalidOperationException>(() => new RailSelector(
            [new StubRail(RailId.Strike, RailLayer.Lightning), new StubRail(RailId.Strike, RailLayer.Lightning)], _health, _options));
}
