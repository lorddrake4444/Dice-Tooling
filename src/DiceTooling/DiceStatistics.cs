using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DiceTooling;

/// <summary>An exact probability distribution over the possible totals of a roll.</summary>
public sealed class Distribution
{
    private readonly Dictionary<int, double> _probabilities;

    /// <summary>Wraps a map of total-to-probability.</summary>
    public Distribution(IReadOnlyDictionary<int, double> probabilities)
    {
        if (probabilities is null)
        {
            throw new ArgumentNullException(nameof(probabilities));
        }

        _probabilities = new Dictionary<int, double>(probabilities);
    }

    /// <summary>The probability of each possible total.</summary>
    public IReadOnlyDictionary<int, double> Probabilities => _probabilities;

    /// <summary>The lowest possible total.</summary>
    public int Minimum => _probabilities.Keys.Min();

    /// <summary>The highest possible total.</summary>
    public int Maximum => _probabilities.Keys.Max();

    /// <summary>The expected value.</summary>
    public double Mean => _probabilities.Sum(pair => pair.Key * pair.Value);

    /// <summary>The variance of the distribution.</summary>
    public double Variance
    {
        get
        {
            double mean = Mean;
            return _probabilities.Sum(pair => pair.Value * (pair.Key - mean) * (pair.Key - mean));
        }
    }

    /// <summary>The standard deviation of the distribution.</summary>
    public double StandardDeviation => Math.Sqrt(Variance);

    /// <summary>The probability of rolling exactly <paramref name="total"/>.</summary>
    public double ProbabilityOf(int total) =>
        _probabilities.TryGetValue(total, out double probability) ? probability : 0.0;

    /// <summary>The probability of rolling at least <paramref name="total"/>.</summary>
    public double AtLeast(int total) =>
        _probabilities.Where(pair => pair.Key >= total).Sum(pair => pair.Value);

    /// <summary>The probability of rolling at most <paramref name="total"/>.</summary>
    public double AtMost(int total) =>
        _probabilities.Where(pair => pair.Key <= total).Sum(pair => pair.Value);

    /// <summary>The distribution, ordered from lowest total to highest.</summary>
    public IEnumerable<KeyValuePair<int, double>> Ordered =>
        _probabilities.OrderBy(pair => pair.Key);

    /// <inheritdoc />
    public override string ToString() =>
        $"mean {Mean.ToString("0.###", CultureInfo.InvariantCulture)}, sd {StandardDeviation.ToString("0.###", CultureInfo.InvariantCulture)}, range {Minimum}..{Maximum}";
}

/// <summary>A summary of a set of observed roll totals, usually from a Monte Carlo estimate.</summary>
public sealed class RollSummary
{
    private readonly Dictionary<int, int> _histogram;

    /// <summary>Builds a summary from a sequence of totals.</summary>
    public RollSummary(IEnumerable<int> totals)
    {
        if (totals is null)
        {
            throw new ArgumentNullException(nameof(totals));
        }

        List<int> samples = totals.ToList();
        if (samples.Count == 0)
        {
            throw new ArgumentException("At least one sample is required.", nameof(totals));
        }

        SampleCount = samples.Count;
        _histogram = new Dictionary<int, int>();

        long sum = 0;
        int minimum = int.MaxValue;
        int maximum = int.MinValue;

        foreach (int value in samples)
        {
            sum += value;
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
            _histogram[value] = _histogram.GetValueOrDefault(value) + 1;
        }

        Mean = (double)sum / SampleCount;
        Minimum = minimum;
        Maximum = maximum;

        double variance = 0.0;
        foreach (int value in samples)
        {
            double delta = value - Mean;
            variance += delta * delta;
        }

        StandardDeviation = Math.Sqrt(variance / SampleCount);
    }

    /// <summary>How many rolls were sampled.</summary>
    public int SampleCount { get; }

    /// <summary>The average total.</summary>
    public double Mean { get; }

    /// <summary>The standard deviation of the totals.</summary>
    public double StandardDeviation { get; }

    /// <summary>The lowest total observed.</summary>
    public int Minimum { get; }

    /// <summary>The highest total observed.</summary>
    public int Maximum { get; }

    /// <summary>How often each total was observed.</summary>
    public IReadOnlyDictionary<int, int> Histogram => _histogram;

    /// <summary>The observed probability of exactly <paramref name="total"/>.</summary>
    public double ProbabilityOf(int total) =>
        _histogram.TryGetValue(total, out int count) ? (double)count / SampleCount : 0.0;

    /// <summary>The observed probability of at least <paramref name="total"/>.</summary>
    public double AtLeast(int total) =>
        _histogram.Where(pair => pair.Key >= total).Sum(pair => pair.Value) / (double)SampleCount;

    /// <summary>The observed probability of at most <paramref name="total"/>.</summary>
    public double AtMost(int total) =>
        _histogram.Where(pair => pair.Key <= total).Sum(pair => pair.Value) / (double)SampleCount;

    /// <inheritdoc />
    public override string ToString() =>
        $"n={SampleCount}, mean {Mean.ToString("0.###", CultureInfo.InvariantCulture)}, sd {StandardDeviation.ToString("0.###", CultureInfo.InvariantCulture)}, range {Minimum}..{Maximum}";
}

/// <summary>Odds and averages for dice rolls.</summary>
public static class DiceStatistics
{
    /// <summary>
    /// Computes the exact distribution of the sum of <paramref name="count"/> dice with
    /// <paramref name="faces"/> faces (no keep/drop or explosions) using dynamic programming.
    /// </summary>
    public static Distribution SumOfDice(int count, int faces)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The number of dice cannot be negative.");
        }

        if (faces < Die.MinFaces)
        {
            throw new ArgumentOutOfRangeException(nameof(faces), faces, $"A die must have at least {Die.MinFaces} faces.");
        }

        var distribution = new Dictionary<int, double> { [0] = 1.0 };

        for (int die = 0; die < count; die++)
        {
            var next = new Dictionary<int, double>();
            foreach (KeyValuePair<int, double> entry in distribution)
            {
                for (int face = 1; face <= faces; face++)
                {
                    int total = entry.Key + face;
                    next[total] = next.GetValueOrDefault(total) + (entry.Value / faces);
                }
            }

            distribution = next;
        }

        return new Distribution(distribution);
    }

    /// <summary>The exact expected sum of <paramref name="count"/> dice with <paramref name="faces"/> faces.</summary>
    public static double MeanOfSum(int count, int faces) => count * ((faces + 1) / 2.0);

    /// <summary>Runs <paramref name="roll"/> a number of times and summarises the totals.</summary>
    public static RollSummary MonteCarlo(Func<RollResult> roll, int iterations)
    {
        if (roll is null)
        {
            throw new ArgumentNullException(nameof(roll));
        }

        if (iterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), iterations, "At least one iteration is required.");
        }

        var totals = new List<int>(iterations);
        for (int i = 0; i < iterations; i++)
        {
            totals.Add(roll().Total);
        }

        return new RollSummary(totals);
    }

    /// <summary>
    /// Monte Carlo estimate for a notation string such as <c>4d6kh3</c>. Optionally seeded for
    /// reproducibility, and optionally with streak prevention applied.
    /// </summary>
    public static RollSummary MonteCarlo(
        string notation,
        int iterations,
        int? seed = null,
        StreakPreventionOptions? streakPrevention = null)
    {
        DiceExpression expression = DiceExpression.Parse(notation);
        IRandomSource random = seed.HasValue ? new RandomSource(seed.Value) : Dice.Random;
        RollOptions options = expression.Options;

        if (streakPrevention is not null)
        {
            options = options with { StreakPrevention = streakPrevention };
        }

        StreakGuard? guard = streakPrevention is null ? null : new StreakGuard();

        return MonteCarlo(
            () => DiceRoller.Roll(expression.Count, expression.Faces, options, expression.Source, random, guard),
            iterations);
    }

    /// <summary>Renders a simple text histogram of a distribution or summary.</summary>
    public static string ToTextHistogram(Distribution distribution, int barWidth = 40)
    {
        if (distribution is null)
        {
            throw new ArgumentNullException(nameof(distribution));
        }

        double peak = distribution.Probabilities.Values.DefaultIfEmpty(0).Max();
        var builder = new StringBuilder();

        foreach (KeyValuePair<int, double> entry in distribution.Ordered)
        {
            int bars = peak <= 0 ? 0 : (int)Math.Round(barWidth * entry.Value / peak);
            builder.Append(entry.Key.ToString(CultureInfo.InvariantCulture).PadLeft(4));
            builder.Append(" | ");
            builder.Append(new string('#', bars));
            builder.Append(' ');
            builder.Append(entry.Value.ToString("0.####", CultureInfo.InvariantCulture));
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
