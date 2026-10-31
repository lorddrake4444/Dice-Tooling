using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceTooling;

/// <summary>
/// The internal rolling pipeline. Everything public funnels through here so that all of the
/// features compose in one predictable order: roll, reroll, explode, clamp, keep/drop, streaks.
/// </summary>
internal static class DiceRoller
{
    public static RollResult Roll(
        int count,
        int faces,
        RollOptions options,
        string expression,
        IRandomSource random,
        StreakGuard? streakGuard)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (random is null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The number of dice cannot be negative.");
        }

        if (faces < Die.MinFaces)
        {
            throw new ArgumentOutOfRangeException(nameof(faces), faces, $"A die must have at least {Die.MinFaces} faces.");
        }

        bool fate = options.FateDie;
        int effectiveFaces = fate ? 3 : faces;

        var rolls = new List<DieRoll>(count);
        for (int i = 0; i < count; i++)
        {
            rolls.Add(new DieRoll(faces, MapValue(random.NextInt(1, effectiveFaces), fate)));
        }

        if (options.Reroll is { } rerollRule)
        {
            ApplyRerolls(rolls, rerollRule, effectiveFaces, fate, random);
        }

        if (options.Explode is { } explodeRule && !fate)
        {
            ApplyExplosions(rolls, explodeRule, effectiveFaces, random);
        }

        ApplyClamp(rolls, options.MinimumValue, options.MaximumValue);
        ApplyKeepDrop(rolls, options.KeepDrop, options.KeepDropCount);

        var adjustments = new List<StreakAdjustment>();
        if (streakGuard is not null && options.StreakPrevention is { } prevention)
        {
            ApplyStreakPrevention(rolls, prevention, effectiveFaces, fate, random, streakGuard, expression, adjustments);
        }

        return new RollResult(expression, rolls, options, adjustments);
    }

    private static int MapValue(int raw, bool fate) => fate ? raw - 2 : raw;

    private static void ApplyRerolls(
        List<DieRoll> rolls,
        RerollRule rule,
        int effectiveFaces,
        bool fate,
        IRandomSource random)
    {
        int budget = rule.MaxRerolls;

        for (int i = 0; i < rolls.Count && budget > 0; i++)
        {
            DieRoll die = rolls[i];
            if (die.IsDropped || !rule.ShouldReroll(die.Value))
            {
                continue;
            }

            int value = die.Value;
            do
            {
                value = MapValue(random.NextInt(1, effectiveFaces), fate);
                budget--;
            }
            while (!rule.Once && budget > 0 && rule.ShouldReroll(value));

            DieRoll updated = die with { Value = value, IsRerolled = true };
            if (rule.DropInsteadOfReroll && rule.ShouldReroll(value))
            {
                updated = updated with { IsDropped = true };
            }

            rolls[i] = updated;
        }
    }

    private static void ApplyExplosions(
        List<DieRoll> rolls,
        ExplodeRule rule,
        int effectiveFaces,
        IRandomSource random)
    {
        var pending = new Stack<(int Value, int Depth)>();
        foreach (DieRoll die in rolls)
        {
            if (!die.IsDropped && rule.ShouldExplode(die.Value, effectiveFaces))
            {
                pending.Push((die.Value, 1));
            }
        }

        int produced = 0;
        while (pending.Count > 0 && produced < rule.MaxExplosions)
        {
            (int _, int depth) = pending.Pop();
            if (depth > rule.MaxDepth)
            {
                continue;
            }

            int raw = random.NextInt(1, effectiveFaces);
            int contribution = rule.Penetrating ? Math.Max(1, raw - 1) : raw;
            rolls.Add(new DieRoll(effectiveFaces, contribution)
            {
                IsExploded = true,
                Note = rule.Penetrating ? $"penetrating (rolled {raw})" : null,
            });
            produced++;

            if (rule.ShouldExplode(raw, effectiveFaces))
            {
                pending.Push((raw, depth + 1));
            }
        }
    }

    private static void ApplyClamp(List<DieRoll> rolls, int? minimum, int? maximum)
    {
        if (minimum is null && maximum is null)
        {
            return;
        }

        for (int i = 0; i < rolls.Count; i++)
        {
            DieRoll die = rolls[i];
            int value = die.Value;

            if (minimum is int low && value < low)
            {
                value = low;
            }

            if (maximum is int high && value > high)
            {
                value = high;
            }

            if (value != die.Value)
            {
                rolls[i] = die with { Value = value, Note = "clamped" };
            }
        }
    }

    private static void ApplyKeepDrop(List<DieRoll> rolls, KeepDropRule rule, int n)
    {
        if (rule == KeepDropRule.None || n <= 0)
        {
            return;
        }

        var active = new List<int>();
        for (int i = 0; i < rolls.Count; i++)
        {
            if (!rolls[i].IsDropped)
            {
                active.Add(i);
            }
        }

        if (active.Count == 0)
        {
            return;
        }

        int dropCount = rule switch
        {
            KeepDropRule.KeepHighest => Math.Max(0, active.Count - n),
            KeepDropRule.KeepLowest => Math.Max(0, active.Count - n),
            KeepDropRule.DropHighest => Math.Min(n, active.Count),
            KeepDropRule.DropLowest => Math.Min(n, active.Count),
            _ => 0,
        };

        if (dropCount == 0)
        {
            return;
        }

        bool dropHigh = rule is KeepDropRule.KeepLowest or KeepDropRule.DropHighest;

        active.Sort((a, b) =>
        {
            int comparison = dropHigh
                ? rolls[b].Value.CompareTo(rolls[a].Value)
                : rolls[a].Value.CompareTo(rolls[b].Value);
            return comparison != 0 ? comparison : a.CompareTo(b);
        });

        for (int k = 0; k < dropCount; k++)
        {
            int index = active[k];
            rolls[index] = rolls[index] with { IsDropped = true };
        }
    }

    private static void ApplyStreakPrevention(
        List<DieRoll> rolls,
        StreakPreventionOptions prevention,
        int effectiveFaces,
        bool fate,
        IRandomSource random,
        StreakGuard guard,
        string expression,
        List<StreakAdjustment> adjustments)
    {
        List<DieRoll> kept = rolls.Where(die => !die.IsDropped).ToList();
        if (kept.Count == 0)
        {
            return;
        }

        double midpoint = fate ? 0.0 : (effectiveFaces + 1) / 2.0;
        if (midpoint <= 0)
        {
            return;
        }

        double average = kept.Average(die => (double)die.Value);
        RollQuality quality = RollQuality.Normal;
        if (average < midpoint * (1 - prevention.Tolerance))
        {
            quality = RollQuality.Low;
        }
        else if (average > midpoint * (1 + prevention.Tolerance))
        {
            quality = RollQuality.High;
        }

        (int lowStreak, int highStreak) = guard.Observe(expression, quality, kept.Sum(die => die.Value));

        bool badStreak = prevention.PreventBadStreaks
            && quality == RollQuality.Low
            && lowStreak >= prevention.StreakLength;
        bool goodStreak = prevention.PreventGoodStreaks
            && quality == RollQuality.High
            && highStreak >= prevention.StreakLength;

        if (badStreak && prevention.BadStreakRemedy != StreakRemedy.None)
        {
            ApplyRemedy(rolls, prevention.BadStreakRemedy, mercy: true, effectiveFaces, fate, random, prevention.MaxRemediesPerRoll, expression, adjustments);
            guard.Reset(expression);
        }
        else if (goodStreak && prevention.GoodStreakRemedy != StreakRemedy.None)
        {
            ApplyRemedy(rolls, prevention.GoodStreakRemedy, mercy: false, effectiveFaces, fate, random, prevention.MaxRemediesPerRoll, expression, adjustments);
            guard.Reset(expression);
        }
    }

    private static void ApplyRemedy(
        List<DieRoll> rolls,
        StreakRemedy remedy,
        bool mercy,
        int effectiveFaces,
        bool fate,
        IRandomSource random,
        int passes,
        string expression,
        List<StreakAdjustment> adjustments)
    {
        int iterations = remedy == StreakRemedy.RerollEntirePool ? 1 : Math.Max(1, passes);

        for (int pass = 0; pass < iterations; pass++)
        {
            int[] targets = remedy == StreakRemedy.RerollEntirePool
                ? Enumerable.Range(0, rolls.Count).Where(i => !rolls[i].IsDropped).ToArray()
                : new[] { FindExtremeKeptIndex(rolls, worst: mercy) };

            foreach (int index in targets)
            {
                if (index < 0)
                {
                    continue;
                }

                DieRoll die = rolls[index];
                int candidate = MapValue(random.NextInt(1, effectiveFaces), fate);
                int chosen = mercy ? Math.Max(die.Value, candidate) : Math.Min(die.Value, candidate);

                if (chosen == die.Value)
                {
                    continue;
                }

                rolls[index] = die with { Value = chosen, IsRerolled = true };
                adjustments.Add(new StreakAdjustment(
                    expression,
                    index,
                    die.Value,
                    chosen,
                    mercy ? "bad-streak mercy" : "good-streak pity"));
            }
        }
    }

    private static int FindExtremeKeptIndex(List<DieRoll> rolls, bool worst)
    {
        int best = -1;
        for (int i = 0; i < rolls.Count; i++)
        {
            if (rolls[i].IsDropped)
            {
                continue;
            }

            if (best < 0)
            {
                best = i;
                continue;
            }

            int comparison = rolls[i].Value.CompareTo(rolls[best].Value);
            if (worst ? comparison < 0 : comparison > 0)
            {
                best = i;
            }
        }

        return best;
    }
}
