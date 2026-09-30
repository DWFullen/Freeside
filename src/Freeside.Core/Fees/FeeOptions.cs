using Microsoft.Extensions.Options;

namespace Freeside.Core.Fees;

/// <summary>The <c>Fees</c> configuration section.</summary>
public sealed class FeeOptions
{
    public const string SectionName = "Fees";

    public const string FakeAdapter = "Fake";

    /// <summary>
    /// <c>Fees:Collector:Adapter</c>: <c>Fake</c> (regtest only) or unset (no fee collection). The
    /// real adapter needs an ACH provider (docs/plans/placeholders.md, ach-provider).
    /// </summary>
    public CollectorOptions Collector { get; set; } = new();

    public sealed class CollectorOptions
    {
        public string? Adapter { get; set; }
    }
}

internal sealed class FeeOptionsValidator : IValidateOptions<FeeOptions>
{
    public ValidateOptionsResult Validate(string? name, FeeOptions options) =>
        string.IsNullOrEmpty(options.Collector.Adapter) || options.Collector.Adapter == FeeOptions.FakeAdapter
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Fees:Collector:Adapter must be '{FeeOptions.FakeAdapter}' or unset. The real ACH adapter doesn't exist yet (docs/plans/placeholders.md, ach-provider).");
}
