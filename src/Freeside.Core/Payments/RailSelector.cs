using Freeside.Core.Monetary;
using Microsoft.Extensions.Options;

namespace Freeside.Core.Payments;

/// <summary>One of a developer's payout destinations: a rail and the developer's account on it.</summary>
public sealed record RailChoice(RailId Rail, string Account);

/// <summary>
/// A developer's rails in priority order, per layer (project.md §4.2, D14). Built from their signed
/// payout config (P4) in Phase 1.
/// </summary>
public sealed record DeveloperRailPlan(string DeveloperId, IReadOnlyList<RailChoice> OnChain, IReadOnlyList<RailChoice> Lightning);

/// <summary>A rail chosen for one layer, and the account it pays into.</summary>
public sealed record RailRoute(IPaymentRail Rail, string Account);

/// <summary>At most one rail per layer. Neither means checkout can't proceed.</summary>
public sealed record RailSelection(RailRoute? OnChain, RailRoute? Lightning)
{
    public bool Any => OnChain is not null || Lightning is not null;
}

/// <summary>
/// Picks the first healthy rail in each layer, in the developer's order (project.md §4.2): the rail is
/// registered, its breaker isn't open, and it hasn't rejected this developer. On-chain is skipped
/// below the minimum order value (project.md §4.3).
/// </summary>
public sealed class RailSelector
{
    private readonly Dictionary<RailId, IPaymentRail> _rails;
    private readonly RailHealth _health;
    private readonly IOptionsMonitor<PaymentOptions> _options;

    public RailSelector(IEnumerable<IPaymentRail> rails, RailHealth health, IOptionsMonitor<PaymentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(rails);
        var list = rails.ToList();
        var duplicates = list.GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => g.Key.Value).ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException($"More than one rail is registered as: {string.Join(", ", duplicates)}.");
        }

        _rails = list.ToDictionary(r => r.Id);
        _health = health;
        _options = options;
    }

    public RailSelection Select(DeveloperRailPlan plan, Money orderValue)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(orderValue);
        if (orderValue.Currency != Currency.Usd)
        {
            throw new ArgumentException("Order values are checked in USD (project.md §4.3, P5).", nameof(orderValue));
        }

        if (orderValue.MinorUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderValue), "Free orders don't use a rail.");
        }

        var onChain = orderValue.MinorUnits >= _options.CurrentValue.OnChainMinimumUsdCents
            ? First(plan.DeveloperId, plan.OnChain, RailLayer.OnChain)
            : null;
        return new RailSelection(onChain, First(plan.DeveloperId, plan.Lightning, RailLayer.Lightning));
    }

    private RailRoute? First(string developerId, IReadOnlyList<RailChoice> choices, RailLayer layer)
    {
        foreach (var choice in choices)
        {
            if (_rails.TryGetValue(choice.Rail, out var rail)
                && rail.Layer == layer
                && _health.IsAvailable(rail.Id)
                && !_health.IsRejected(developerId, rail.Id))
            {
                return new RailRoute(rail, choice.Account);
            }
        }

        return null;
    }
}
