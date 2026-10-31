using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceTooling;

/// <summary>
/// Configures "streak prevention": the optional, opt-in behaviour that nudges a series of rolls
/// back towards average when the same kind of result keeps coming up.
/// </summary>
/// <remarks>
/// <para>
/// A roll is classified by comparing the average of the kept dice with the midpoint of the die
/// (<c>(faces + 1) / 2</c>). If the average is below <c>midpoint * (1 - Tolerance)</c> the roll is
/// counted as a low roll; if it is above <c>midpoint * (1 + Tolerance)</c> it is counted as high.
/// </para>
/// <para>
/// Consecutive low rolls build a "bad streak" and consecutive high rolls build a "good streak".
/// Once a streak reaches <see cref="StreakLength"/> the matching remedy fires and the streak
/// counter for that kind resets. This deliberately bends the probability curve a little, so it is
/// off unless you ask for it.
/// </para>
/// </remarks>
public sealed record StreakPreventionOptions
{
    /// <summary>Fire a remedy after a run of below-average rolls. Defaults to true.</summary>
    public bool PreventBadStreaks { get; init; } = true;

    /// <summary>Fire a remedy after a run of above-average rolls. Defaults to true.</summary>
    public bool PreventGoodStreaks { get; init; } = true;

    /// <summary>How many consecutive rolls of the same kind are needed before a remedy fires. Defaults to 3.</summary>
    public int StreakLength { get; init; } = 3;

    /// <summary>
    /// Fractional band around average that counts as "normal". A value of 0.1 treats anything within
    /// +/-10% of the midpoint as average. Defaults to 0.1.
    /// </summary>
    public double Tolerance { get; init; } = 0.1;

    /// <summary>The remedy applied to a bad streak. Defaults to <see cref="StreakRemedy.RerollWorstDie"/>.</summary>
    public StreakRemedy BadStreakRemedy { get; init; } = StreakRemedy.RerollWorstDie;

    /// <summary>The remedy applied to a good streak. Defaults to <see cref="StreakRemedy.RerollBestDie"/>.</summary>
    public StreakRemedy GoodStreakRemedy { get; init; } = StreakRemedy.RerollBestDie;

    /// <summary>Maximum number of individual dice touched per roll. Defaults to 1.</summary>
    public int MaxRemediesPerRoll { get; init; } = 1;
}

/// <summary>A record of one streak-prevention change made to a roll.</summary>
/// <param name="Expression">The expression the roll belonged to.</param>
/// <param name="DieIndex">The index of the affected die within <see cref="RollResult.Rolls"/>.</param>
/// <param name="OldValue">The value before the adjustment.</param>
/// <param name="NewValue">The value after the adjustment.</param>
/// <param name="Reason">A short explanation (<c>"bad-streak mercy"</c> or <c>"good-streak pity"</c>).</param>
public sealed record StreakAdjustment(string Expression, int DieIndex, int OldValue, int NewValue, string Reason)
{
    /// <inheritdoc />
    public override string ToString() =>
        $"die #{DieIndex}: {OldValue} -> {NewValue} ({Reason})";
}

/// <summary>An immutable snapshot of the streak state tracked for a single expression.</summary>
/// <param name="TotalRolls">How many rolls have been observed.</param>
/// <param name="LowRolls">How many of those were below average.</param>
/// <param name="HighRolls">How many of those were above average.</param>
/// <param name="CurrentLowStreak">The current run of consecutive low rolls.</param>
/// <param name="CurrentHighStreak">The current run of consecutive high rolls.</param>
/// <param name="RecentTotals">The most recent totals, oldest first.</param>
public sealed record StreakSnapshot(
    int TotalRolls,
    int LowRolls,
    int HighRolls,
    int CurrentLowStreak,
    int CurrentHighStreak,
    IReadOnlyList<int> RecentTotals);

/// <summary>
/// Remembers the recent history of rolls, keyed by expression, so that
/// <see cref="StreakPreventionOptions"/> can detect runs of good or bad luck.
/// Safe to share between threads. Create one per logical "session" (a player, a campaign).
/// </summary>
public sealed class StreakGuard
{
    private readonly object _gate = new();
    private readonly Dictionary<string, MutableState> _states = new(StringComparer.Ordinal);
    private readonly int _historyLimit;

    /// <summary>Creates a guard that remembers the most recent <paramref name="historyLimit"/> totals per expression.</summary>
    public StreakGuard(int historyLimit = 20)
    {
        if (historyLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(historyLimit), historyLimit, "History limit cannot be negative.");
        }

        _historyLimit = historyLimit;
    }

    /// <summary>
    /// Records an observation and returns the updated consecutive low/high streak lengths.
    /// </summary>
    /// <param name="key">The expression the roll belongs to.</param>
    /// <param name="quality">How the roll was classified.</param>
    /// <param name="total">The total of the roll, kept for the recent-history log.</param>
    /// <returns>The current <c>(Low, High)</c> streak lengths after the observation.</returns>
    public (int Low, int High) Observe(string key, RollQuality quality, int total)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("A key is required.", nameof(key));
        }

        lock (_gate)
        {
            if (!_states.TryGetValue(key, out MutableState? state))
            {
                state = new MutableState();
                _states[key] = state;
            }

            switch (quality)
            {
                case RollQuality.Low:
                    state.LowStreak++;
                    state.HighStreak = 0;
                    state.LowRolls++;
                    break;
                case RollQuality.High:
                    state.HighStreak++;
                    state.LowStreak = 0;
                    state.HighRolls++;
                    break;
                default:
                    state.LowStreak = 0;
                    state.HighStreak = 0;
                    break;
            }

            state.TotalRolls++;
            if (_historyLimit > 0)
            {
                state.History.Enqueue(total);
                while (state.History.Count > _historyLimit)
                {
                    state.History.Dequeue();
                }
            }

            return (state.LowStreak, state.HighStreak);
        }
    }

    /// <summary>Clears the current streak counters for an expression (used after a remedy fires).</summary>
    public void Reset(string key)
    {
        lock (_gate)
        {
            if (_states.TryGetValue(key, out MutableState? state))
            {
                state.LowStreak = 0;
                state.HighStreak = 0;
            }
        }
    }

    /// <summary>Forgets everything for every expression.</summary>
    public void ResetAll()
    {
        lock (_gate)
        {
            _states.Clear();
        }
    }

    /// <summary>Returns a snapshot of the tracked state for an expression, or <c>null</c> if it is unknown.</summary>
    public StreakSnapshot? GetSnapshot(string key)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(key, out MutableState? state))
            {
                return null;
            }

            return new StreakSnapshot(
                state.TotalRolls,
                state.LowRolls,
                state.HighRolls,
                state.LowStreak,
                state.HighStreak,
                state.History.ToArray());
        }
    }

    private sealed class MutableState
    {
        public int TotalRolls;
        public int LowRolls;
        public int HighRolls;
        public int LowStreak;
        public int HighStreak;
        public Queue<int> History { get; } = new();
    }
}
