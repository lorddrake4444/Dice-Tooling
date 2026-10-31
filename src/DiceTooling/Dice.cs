using System;
using System.Collections.Generic;

namespace DiceTooling;

/// <summary>
/// A stateful dice session: it owns a random source and a <see cref="StreakGuard"/>, so a series
/// of rolls can be seeded, inspected and streak-corrected together. Prefer this over the static
/// <see cref="Dice"/> helpers when you care about reproducibility or streak prevention.
/// </summary>
public sealed class DiceSession
{
    /// <summary>Creates a session with the given random source and streak guard (both optional).</summary>
    public DiceSession(IRandomSource? random = null, StreakGuard? streakGuard = null)
    {
        Random = random ?? new RandomSource();
        StreakGuard = streakGuard ?? new StreakGuard();
    }

    /// <summary>Creates a seeded, reproducible session.</summary>
    public static DiceSession WithSeed(int seed, int historyLimit = 20) =>
        new(new RandomSource(seed), new StreakGuard(historyLimit));

    /// <summary>The random source used by this session.</summary>
    public IRandomSource Random { get; }

    /// <summary>The streak guard used by this session.</summary>
    public StreakGuard StreakGuard { get; }

    /// <summary>Rolls <paramref name="count"/> dice with <paramref name="faces"/> faces.</summary>
    public RollResult Roll(int count, int faces, RollOptions? options = null)
    {
        RollOptions effective = options ?? RollOptions.Default;
        return DiceRoller.Roll(count, faces, effective, Notation.Describe(count, faces, effective), Random, StreakGuard);
    }

    /// <summary>Parses and rolls dice notation such as <c>4d6kh3+2</c>.</summary>
    public RollResult Roll(string notation)
    {
        DiceExpression expression = DiceExpression.Parse(notation);
        return DiceRoller.Roll(expression.Count, expression.Faces, expression.Options, expression.Source, Random, StreakGuard);
    }

    /// <summary>
    /// Parses notation, lets <paramref name="configure"/> adjust the decoded options, then rolls.
    /// </summary>
    public RollResult Roll(string notation, Func<RollOptions, RollOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        DiceExpression expression = DiceExpression.Parse(notation);
        RollOptions options = configure(expression.Options);
        return DiceRoller.Roll(expression.Count, expression.Faces, options, expression.Source, Random, StreakGuard);
    }

    /// <summary>Rolls a single die and returns just its value.</summary>
    public int RollDie(int faces) => Random.NextInt(1, faces);

    /// <summary>Rolls a d20, optionally with advantage or disadvantage.</summary>
    public RollResult D20(bool advantage = false, bool disadvantage = false, int modifier = 0)
    {
        if (advantage && disadvantage)
        {
            throw new ArgumentException("A roll cannot have both advantage and disadvantage.");
        }

        if (advantage)
        {
            return Roll(2, 20, RollOptions.KeepHighest(1) with { Modifier = modifier });
        }

        if (disadvantage)
        {
            return Roll(2, 20, RollOptions.KeepLowest(1) with { Modifier = modifier });
        }

        return Roll(1, 20, RollOptions.WithModifier(modifier));
    }

    /// <summary>Rolls a classic ability score: 4d6 keep highest 3.</summary>
    public RollResult AbilityScore(int modifier = 0) =>
        Roll(4, 6, RollOptions.KeepHighest(3) with { Modifier = modifier });

    /// <summary>Returns a snapshot of the streak state for an expression, if any.</summary>
    public StreakSnapshot? GetStreakSnapshot(string expression) => StreakGuard.GetSnapshot(expression);

    /// <summary>Convenience alias for <see cref="StreakGuard.Reset(string)"/>.</summary>
    public void ResetStreak(string expression) => StreakGuard.Reset(expression);
}

/// <summary>
/// Process-wide convenience entry points. Rolls use <see cref="Random"/> and the shared
/// <see cref="StreakGuard"/>. For anything stateful or reproducible, use <see cref="DiceSession"/>.
/// </summary>
public static class Dice
{
    private static IRandomSource _random = new RandomSource();

    /// <summary>The random source used by the static helpers. Replace it to inject your own.</summary>
    public static IRandomSource Random
    {
        get => _random;
        set => _random = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The streak guard shared by the static helpers.</summary>
    public static StreakGuard StreakGuard { get; } = new();

    /// <summary>
    /// Rolls <paramref name="count"/> dice with <paramref name="faces"/> faces in a single call.
    /// </summary>
    /// <example><c>Dice.Roll(4, 6, RollOptions.KeepHighest(3))</c></example>
    public static RollResult Roll(int count, int faces, RollOptions? options = null)
    {
        RollOptions effective = options ?? RollOptions.Default;
        return DiceRoller.Roll(count, faces, effective, Notation.Describe(count, faces, effective), Random, StreakGuard);
    }

    /// <summary>
    /// Parses and rolls dice notation in a single call, for example <c>Dice.Roll("4d6kh3+2")</c>.
    /// </summary>
    public static RollResult Roll(string notation)
    {
        DiceExpression expression = DiceExpression.Parse(notation);
        return DiceRoller.Roll(expression.Count, expression.Faces, expression.Options, expression.Source, Random, StreakGuard);
    }

    /// <summary>Parses notation, lets <paramref name="configure"/> adjust the options, then rolls.</summary>
    public static RollResult Roll(string notation, Func<RollOptions, RollOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        DiceExpression expression = DiceExpression.Parse(notation);
        RollOptions options = configure(expression.Options);
        return DiceRoller.Roll(expression.Count, expression.Faces, options, expression.Source, Random, StreakGuard);
    }

    /// <summary>Rolls many dice of the same kind at once.</summary>
    public static IReadOnlyList<int> RollMany(int count, int faces, IRandomSource? random = null)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The number of dice cannot be negative.");
        }

        if (faces < Die.MinFaces)
        {
            throw new ArgumentOutOfRangeException(nameof(faces), faces, $"A die must have at least {Die.MinFaces} faces.");
        }

        IRandomSource source = random ?? Random;
        var values = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            values.Add(source.NextInt(1, faces));
        }

        return values;
    }

    /// <summary>Rolls a single die and returns just its value.</summary>
    public static int RollDie(int faces) => RollDie(faces, Random);

    /// <summary>Rolls a single die with an explicit random source.</summary>
    public static int RollDie(int faces, IRandomSource random)
    {
        if (random is null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        if (faces < Die.MinFaces)
        {
            throw new ArgumentOutOfRangeException(nameof(faces), faces, $"A die must have at least {Die.MinFaces} faces.");
        }

        return random.NextInt(1, faces);
    }

    /// <summary>Rolls a d20, optionally with advantage or disadvantage.</summary>
    public static RollResult D20(bool advantage = false, bool disadvantage = false, int modifier = 0)
    {
        if (advantage && disadvantage)
        {
            throw new ArgumentException("A roll cannot have both advantage and disadvantage.");
        }

        if (advantage)
        {
            return Roll(2, 20, RollOptions.KeepHighest(1) with { Modifier = modifier });
        }

        if (disadvantage)
        {
            return Roll(2, 20, RollOptions.KeepLowest(1) with { Modifier = modifier });
        }

        return Roll(1, 20, RollOptions.WithModifier(modifier));
    }

    /// <summary>Rolls a classic ability score: 4d6 keep highest 3.</summary>
    public static RollResult AbilityScore(int modifier = 0) =>
        Roll(4, 6, RollOptions.KeepHighest(3) with { Modifier = modifier });

    /// <summary>Rolls a commonly requested dice pool such as <c>8d6</c>.</summary>
    public static RollResult Pool(int count, int faces, int targetNumber, Comparison comparison = Comparison.AtLeast) =>
        Roll(count, faces, RollOptions.Pool(targetNumber, comparison));

    /// <summary>Parses notation without rolling.</summary>
    public static DiceExpression Parse(string notation) => DiceExpression.Parse(notation);
}
