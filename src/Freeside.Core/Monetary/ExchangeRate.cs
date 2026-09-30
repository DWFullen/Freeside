using System.Globalization;
using System.Numerics;

namespace Freeside.Core.Monetary;

/// <summary>
/// The price of one bitcoin in <see cref="Quote"/>, as a plain decimal string, with where it came
/// from and when (AGENTS.md §2, invariant 5, and §4.3). The string is stored as given and never
/// passes through floating point; conversions use it as an exact fraction.
/// </summary>
public sealed record ExchangeRate
{
    private const int _maxDigitsPerPart = 18;

    public ExchangeRate(string rate, Currency quote, string source, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(quote);
        (Numerator, Denominator) = Parse(rate);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("The rate source is required.", nameof(source));
        }

        if (asOf.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The rate timestamp must be in UTC.", nameof(asOf));
        }

        Rate = rate;
        Quote = quote;
        Source = source;
        AsOf = asOf;
    }

    /// <summary>Units of <see cref="Quote"/> per bitcoin, for example "65000.12".</summary>
    public string Rate { get; }

    public Currency Quote { get; }

    /// <summary>Where the rate came from. A rate is never taken from an unrecorded source.</summary>
    public string Source { get; }

    public DateTimeOffset AsOf { get; }

    /// <summary><see cref="Rate"/> as the exact fraction <see cref="Numerator"/> / <see cref="Denominator"/>.</summary>
    internal BigInteger Numerator { get; }

    internal BigInteger Denominator { get; }

    public override string ToString() => $"{Rate} {Quote.Code}/BTC ({Source}, {AsOf:O})";

    // Accepts only ASCII digits with an optional single decimal point between digits: no sign,
    // exponent, separators, whitespace or non-ASCII digits. Zero is rejected.
    private static (BigInteger Numerator, BigInteger Denominator) Parse(string? rate)
    {
        ArgumentNullException.ThrowIfNull(rate);
        var point = rate.IndexOf('.', StringComparison.Ordinal);
        var whole = point < 0 ? rate : rate[..point];
        var fraction = point < 0 ? "" : rate[(point + 1)..];

        if (!IsDigits(whole) || (point >= 0 && !IsDigits(fraction)))
        {
            throw new FormatException($"'{rate}' is not a plain decimal rate such as \"65000.12\".");
        }

        var numerator = BigInteger.Parse(whole + fraction, NumberStyles.None, CultureInfo.InvariantCulture);
        if (numerator.IsZero)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "The rate must be greater than zero.");
        }

        return (numerator, BigInteger.Pow(10, fraction.Length));
    }

    private static bool IsDigits(string part) =>
        part.Length is > 0 and <= _maxDigitsPerPart && part.All(c => c is >= '0' and <= '9');
}
