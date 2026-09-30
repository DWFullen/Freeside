namespace Freeside.Core.Monetary;

/// <summary>
/// How a conversion rounds when the exact result isn't a whole number of the target unit. Every
/// conversion takes one explicitly (AGENTS.md §4.3); nothing rounds implicitly. Amounts in
/// conversions are never negative, so "toward zero" is the floor and "away from zero" the ceiling.
/// </summary>
public enum RoundingRule
{
    /// <summary>Toward zero: the result never exceeds the exact value.</summary>
    Down = 1,

    /// <summary>Away from zero: the result is never less than the exact value.</summary>
    Up = 2,

    /// <summary>To the nearest whole unit; an exact half goes to the even neighbour.</summary>
    HalfEven = 3,
}
