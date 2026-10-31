namespace DiceTooling;

/// <summary>
/// Everything that can be configured about a single roll. Immutable: use <c>with</c> to derive
/// a variation from an existing instance.
/// </summary>
public sealed record RollOptions
{
    /// <summary>Options with every feature disabled.</summary>
    public static RollOptions Default { get; } = new();

    /// <summary>How the pool is trimmed before totalling. Defaults to <see cref="KeepDropRule.None"/>.</summary>
    public KeepDropRule KeepDrop { get; init; } = KeepDropRule.None;

    /// <summary>The N used by <see cref="KeepDrop"/>. Ignored when <see cref="KeepDrop"/> is <see cref="KeepDropRule.None"/>.</summary>
    public int KeepDropCount { get; init; }

    /// <summary>A flat number added to (or subtracted from) the total.</summary>
    public int Modifier { get; init; }

    /// <summary>Raises any die below this value up to it (notation <c>4d6min2</c>).</summary>
    public int? MinimumValue { get; init; }

    /// <summary>Lowers any die above this value down to it (notation <c>4d6max5</c>).</summary>
    public int? MaximumValue { get; init; }

    /// <summary>Exploding-dice rule, or <c>null</c> for no explosions.</summary>
    public ExplodeRule? Explode { get; init; }

    /// <summary>Reroll rule, or <c>null</c> for no rerolls.</summary>
    public RerollRule? Reroll { get; init; }

    /// <summary>
    /// When set, dice are not summed; instead the number of kept dice satisfying
    /// <see cref="SuccessComparison"/> against this target is counted.
    /// </summary>
    public int? TargetNumber { get; init; }

    /// <summary>The comparison used when <see cref="TargetNumber"/> is set. Defaults to at-least.</summary>
    public Comparison SuccessComparison { get; init; } = Comparison.AtLeast;

    /// <summary>When true, kept dice are reported highest-first.</summary>
    public bool SortResults { get; init; }

    /// <summary>When true the die is a Fudge/Fate die (values -1, 0, +1).</summary>
    public bool FateDie { get; init; }

    /// <summary>Optional bad/good streak prevention settings.</summary>
    public StreakPreventionOptions? StreakPrevention { get; init; }

    /// <summary>True when the roll counts successes instead of summing dice.</summary>
    public bool CountsSuccesses => TargetNumber.HasValue;

    /// <summary>Keeps only the highest <paramref name="count"/> dice (4d6 keep 3).</summary>
    public static RollOptions KeepHighest(int count) =>
        new() { KeepDrop = KeepDropRule.KeepHighest, KeepDropCount = count };

    /// <summary>Keeps only the lowest <paramref name="count"/> dice.</summary>
    public static RollOptions KeepLowest(int count) =>
        new() { KeepDrop = KeepDropRule.KeepLowest, KeepDropCount = count };

    /// <summary>Removes the highest <paramref name="count"/> dice.</summary>
    public static RollOptions DropHighest(int count) =>
        new() { KeepDrop = KeepDropRule.DropHighest, KeepDropCount = count };

    /// <summary>Removes the lowest <paramref name="count"/> dice.</summary>
    public static RollOptions DropLowest(int count) =>
        new() { KeepDrop = KeepDropRule.DropLowest, KeepDropCount = count };

    /// <summary>Options carrying just a flat modifier.</summary>
    public static RollOptions WithModifier(int modifier) => new() { Modifier = modifier };

    /// <summary>Options that count successes against a target number.</summary>
    public static RollOptions Pool(int targetNumber, Comparison comparison = Comparison.AtLeast) =>
        new() { TargetNumber = targetNumber, SuccessComparison = comparison };
}
