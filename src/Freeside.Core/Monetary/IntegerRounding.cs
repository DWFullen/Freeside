using System.Numerics;

namespace Freeside.Core.Monetary;

internal static class IntegerRounding
{
    /// <summary>Divides a non-negative numerator by a positive denominator, rounding by <paramref name="rule"/>.</summary>
    public static BigInteger Divide(BigInteger numerator, BigInteger denominator, RoundingRule rule)
    {
        if (numerator.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator), numerator, "Must not be negative.");
        }

        if (denominator.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), denominator, "Must be positive.");
        }

        var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
        if (remainder.IsZero)
        {
            return quotient;
        }

        return rule switch
        {
            RoundingRule.Down => quotient,
            RoundingRule.Up => quotient + 1,
            RoundingRule.HalfEven => BigInteger.Compare(remainder * 2, denominator) switch
            {
                < 0 => quotient,
                > 0 => quotient + 1,
                _ => quotient.IsEven ? quotient : quotient + 1,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unknown rounding rule."),
        };
    }

    /// <summary>Narrows to <see cref="long"/>, throwing <see cref="OverflowException"/> if it doesn't fit.</summary>
    public static long ToInt64(BigInteger value, string what) =>
        value >= long.MinValue && value <= long.MaxValue
            ? (long)value
            : throw new OverflowException($"{what} {value} doesn't fit in a 64-bit integer.");
}
