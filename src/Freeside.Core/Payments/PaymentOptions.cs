using Microsoft.Extensions.Options;

namespace Freeside.Core.Payments;

/// <summary>The <c>Payments</c> configuration section, validated when the host starts.</summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>On-chain is offered only at or above this order value, in US cents (project.md §4.3: $10).</summary>
    public long OnChainMinimumUsdCents { get; set; } = 1_000;

    public BreakerOptions Breaker { get; set; } = new();

    /// <summary>Per-rail settings by rail ID, for example <c>Payments:Rails:strike:Disabled</c>.</summary>
    public Dictionary<string, RailOptions> Rails { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public StrikeOptions Strike { get; set; } = new();
}

public sealed class BreakerOptions
{
    /// <summary>Consecutive failures that open a rail's breaker.</summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>How long an open breaker waits before letting a trial request through.</summary>
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Consecutive successes, while half-open, that close the breaker.</summary>
    public int SuccessThreshold { get; set; } = 3;

    /// <summary>How long a rail stays off for one developer after it rejects their account.</summary>
    public TimeSpan AccountRejectionTtl { get; set; } = TimeSpan.FromHours(1);
}

public sealed class RailOptions
{
    /// <summary>The kill switch: the rail is off for every developer (project.md §4.2).</summary>
    public bool Disabled { get; set; }
}

public sealed class StrikeOptions
{
    public const string FakeAdapter = "Fake";

    /// <summary>
    /// <c>Fake</c> (regtest only) or unset (the Strike rail is off). The real adapter needs a Strike
    /// account (docs/plans/placeholders.md, strike-api).
    /// </summary>
    public string? Adapter { get; set; }
}

internal sealed class PaymentOptionsValidator : IValidateOptions<PaymentOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentOptions options)
    {
        var failures = new List<string>();
        if (options.OnChainMinimumUsdCents < 0)
        {
            failures.Add("Payments:OnChainMinimumUsdCents must not be negative.");
        }

        var breaker = options.Breaker;
        if (breaker.FailureThreshold is < 1 or > 100 || breaker.SuccessThreshold is < 1 or > 100)
        {
            failures.Add("Payments:Breaker:FailureThreshold and SuccessThreshold must be between 1 and 100.");
        }

        if (breaker.Cooldown <= TimeSpan.Zero || breaker.AccountRejectionTtl <= TimeSpan.Zero)
        {
            failures.Add("Payments:Breaker:Cooldown and AccountRejectionTtl must be more than 0.");
        }

        foreach (var rail in options.Rails.Keys.Where(key => !IsRailId(key)))
        {
            failures.Add($"Payments:Rails:{rail} is not a valid rail ID.");
        }

        if (!string.IsNullOrEmpty(options.Strike.Adapter) && options.Strike.Adapter != StrikeOptions.FakeAdapter)
        {
            failures.Add($"Payments:Strike:Adapter must be '{StrikeOptions.FakeAdapter}' or unset. The real Strike adapter doesn't exist yet (docs/plans/placeholders.md, strike-api).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsRailId(string value)
    {
        try
        {
            _ = new RailId(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
