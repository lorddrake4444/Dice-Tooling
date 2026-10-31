using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DiceTooling;

/// <summary>
/// The complete outcome of a roll: every die that was rolled, which dice were kept or dropped,
/// the final total, and any streak-prevention adjustments that were applied.
/// </summary>
public sealed class RollResult
{
    internal RollResult(
        string expression,
        IReadOnlyList<DieRoll> rolls,
        RollOptions options,
        IReadOnlyList<StreakAdjustment> adjustments)
    {
        Expression = expression;
        Options = options;
        Rolls = rolls.ToArray();
        Adjustments = adjustments.ToArray();

        IEnumerable<DieRoll> kept = Rolls.Where(die => !die.IsDropped);
        Kept = (options.SortResults ? kept.OrderByDescending(die => die.Value) : kept).ToArray();
        Dropped = Rolls.Where(die => die.IsDropped).ToArray();

        KeptSum = Kept.Sum(die => die.Value);

        if (options.TargetNumber is int target)
        {
            Successes = Kept.Count(die => Comparisons.Satisfies(die.Value, target, options.SuccessComparison));
            Total = Successes.Value + options.Modifier;
        }
        else
        {
            Successes = null;
            Total = KeptSum + options.Modifier;
        }
    }

    /// <summary>The notation that produced this roll (for example <c>4d6kh3+2</c>).</summary>
    public string Expression { get; }

    /// <summary>The options used for the roll.</summary>
    public RollOptions Options { get; }

    /// <summary>Every die that was rolled, in the order it was rolled (including dropped and exploded dice).</summary>
    public IReadOnlyList<DieRoll> Rolls { get; }

    /// <summary>The dice that survived keep/drop rules. Highest-first when <see cref="RollOptions.SortResults"/> is set.</summary>
    public IReadOnlyList<DieRoll> Kept { get; }

    /// <summary>The dice removed by keep/drop or reroll rules.</summary>
    public IReadOnlyList<DieRoll> Dropped { get; }

    /// <summary>Streak-prevention remedies that were applied to this roll.</summary>
    public IReadOnlyList<StreakAdjustment> Adjustments { get; }

    /// <summary>The sum of the kept dice, before any modifier.</summary>
    public int KeptSum { get; }

    /// <summary>The number of successes, when the roll counts successes; otherwise <c>null</c>.</summary>
    public int? Successes { get; }

    /// <summary>
    /// The final result: the kept sum plus the modifier, or (when counting successes) the number
    /// of successes plus the modifier.
    /// </summary>
    public int Total { get; }

    /// <summary>The flat modifier that was applied.</summary>
    public int Modifier => Options.Modifier;

    /// <summary>True when no dice were kept (an empty pool).</summary>
    public bool IsEmpty => Kept.Count == 0;

    /// <summary>True when <see cref="Total"/> is greater than or equal to <paramref name="threshold"/>.</summary>
    public bool MeetsAtLeast(int threshold) => Total >= threshold;

    /// <summary>
    /// True when every kept die shows its maximum face — a "critical" on most systems.
    /// Returns <c>false</c> for an empty pool.
    /// </summary>
    public bool IsAllMaximum() => Kept.Count > 0 && Kept.All(die => die.Value >= die.Faces);

    /// <summary>
    /// True when every kept die shows a value of 1 — a "fumble" on most systems.
    /// Returns <c>false</c> for an empty pool.
    /// </summary>
    public bool IsAllOnes() => Kept.Count > 0 && Kept.All(die => die.Value == 1);

    /// <summary>A compact one-line summary such as <c>4d6kh3+2 = 15 [6, 5, 4 | dropped: 2]</c>.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(Expression).Append(" = ").Append(Total.ToString(CultureInfo.InvariantCulture));
        builder.Append(" [").Append(string.Join(", ", Kept.Select(die => die.Value)));

        if (Dropped.Count > 0)
        {
            builder.Append(" | dropped: ").Append(string.Join(", ", Dropped.Select(die => die.Value)));
        }

        builder.Append(']');

        if (Successes.HasValue)
        {
            builder.Append(" (").Append(Successes.Value).Append(" successes)");
        }

        return builder.ToString();
    }

    /// <summary>A multi-line, human-readable breakdown including dice flags and adjustments.</summary>
    public string Describe()
    {
        var builder = new StringBuilder();
        builder.AppendLine(ToString());
        builder.AppendLine("  kept:    " + string.Join(", ", Kept.Select(DescribeDie)));
        builder.AppendLine("  dropped: " + string.Join(", ", Dropped.Select(DescribeDie)));

        foreach (StreakAdjustment adjustment in Adjustments)
        {
            builder.AppendLine("  adjust:  " + adjustment);
        }

        return builder.ToString().TrimEnd();
    }

    private static string DescribeDie(DieRoll die)
    {
        var flags = new List<string>();
        if (die.IsExploded)
        {
            flags.Add("exploded");
        }

        if (die.IsRerolled)
        {
            flags.Add("rerolled");
        }

        if (die.Note is { Length: > 0 })
        {
            flags.Add(die.Note!);
        }

        return flags.Count == 0
            ? die.Value.ToString(CultureInfo.InvariantCulture)
            : $"{die.Value} ({string.Join(", ", flags)})";
    }
}
