namespace DiceTooling;

/// <summary>Helpers for the <see cref="Comparison"/> enum.</summary>
internal static class Comparisons
{
    /// <summary>Evaluates <paramref name="value"/> against <paramref name="target"/>.</summary>
    public static bool Satisfies(int value, int target, Comparison comparison) => comparison switch
    {
        Comparison.AtLeast => value >= target,
        Comparison.AtMost => value <= target,
        Comparison.GreaterThan => value > target,
        Comparison.LessThan => value < target,
        Comparison.Equal => value == target,
        Comparison.NotEqual => value != target,
        _ => false,
    };

    /// <summary>Returns the notation symbol for a comparison.</summary>
    public static string Symbol(Comparison comparison) => comparison switch
    {
        Comparison.AtLeast => ">=",
        Comparison.AtMost => "<=",
        Comparison.GreaterThan => ">",
        Comparison.LessThan => "<",
        Comparison.Equal => "=",
        Comparison.NotEqual => "!=",
        _ => "?",
    };
}
