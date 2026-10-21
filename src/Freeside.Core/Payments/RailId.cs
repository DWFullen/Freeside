using System.Text.RegularExpressions;

namespace Freeside.Core.Payments;

/// <summary>Identifies a payment rail: <c>btcpay-onchain</c>, <c>strike</c>, <c>lnurl-verify</c>, …</summary>
public readonly partial record struct RailId
{
    public static readonly RailId BtcpayOnchain = new("btcpay-onchain");
    public static readonly RailId Strike = new("strike");
    public static readonly RailId LnurlVerify = new("lnurl-verify");

    public RailId(string value)
    {
        if (value is null || !Format().IsMatch(value))
        {
            throw new ArgumentException("A rail ID is 1 to 32 lowercase letters, digits and hyphens.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}

/// <summary>A payment layer. Checkout picks at most one rail per layer (project.md §4.2).</summary>
public enum RailLayer
{
    OnChain = 1,
    Lightning = 2,
}
