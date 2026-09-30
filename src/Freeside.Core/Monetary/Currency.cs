using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Freeside.Core.Monetary;

/// <summary>
/// An ISO 4217 currency and the number of decimal places in its minor unit. Only currencies in the
/// table below exist; anything else is rejected rather than guessed.
/// </summary>
public sealed record Currency
{
    // ISO 4217 minor-unit exponents. JPY (0) and KWD/BHD (3) are here so every exponent the
    // conversion code handles is exercised by tests. Add a currency here before accepting it.
    private static readonly FrozenDictionary<string, Currency> _known = new Dictionary<string, Currency>(StringComparer.Ordinal)
    {
        ["USD"] = new("USD", 2),
        ["EUR"] = new("EUR", 2),
        ["GBP"] = new("GBP", 2),
        ["CAD"] = new("CAD", 2),
        ["AUD"] = new("AUD", 2),
        ["CHF"] = new("CHF", 2),
        ["JPY"] = new("JPY", 0),
        ["KWD"] = new("KWD", 3),
        ["BHD"] = new("BHD", 3),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private Currency(string code, int minorUnitExponent)
    {
        Code = code;
        MinorUnitExponent = minorUnitExponent;
    }

    public static Currency Usd { get; } = FromCode("USD");

    /// <summary>The three-letter uppercase ISO 4217 code.</summary>
    public string Code { get; }

    /// <summary>Decimal places in the minor unit: 2 for USD (cents), 0 for JPY.</summary>
    public int MinorUnitExponent { get; }

    /// <summary>Exact, case-sensitive lookup: "usd" is rejected.</summary>
    public static bool TryFromCode(string? code, [NotNullWhen(true)] out Currency? currency)
    {
        currency = null;
        return code is not null && _known.TryGetValue(code, out currency);
    }

    public static Currency FromCode(string code) =>
        TryFromCode(code, out var currency)
            ? currency
            : throw new ArgumentException($"'{code}' is not a supported ISO 4217 currency code.", nameof(code));

    public override string ToString() => Code;
}
