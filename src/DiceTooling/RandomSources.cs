using System;
using System.Collections.Generic;

namespace DiceTooling;

/// <summary>
/// The default <see cref="IRandomSource"/>, backed by <see cref="System.Random"/>.
/// A single instance is safe to share between threads.
/// </summary>
public sealed class RandomSource : IRandomSource
{
    private readonly Random _random;
    private readonly object _gate = new();

    /// <summary>Creates a source backed by the shared, thread-safe <see cref="System.Random.Shared"/> instance.</summary>
    public RandomSource()
        : this(Random.Shared)
    {
    }

    /// <summary>Creates a deterministic source from the given seed.</summary>
    public RandomSource(int seed)
        : this(new Random(seed))
    {
    }

    /// <summary>Creates a source wrapping an existing <see cref="System.Random"/>.</summary>
    public RandomSource(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <inheritdoc />
    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (minInclusive > maxInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minInclusive),
                minInclusive,
                "minInclusive must be less than or equal to maxInclusive.");
        }

        lock (_gate)
        {
            // Random.Next's upper bound is exclusive, so add one.
            long exclusive = (long)maxInclusive + 1L;
            if (exclusive <= int.MaxValue)
            {
                return _random.Next(minInclusive, (int)exclusive);
            }

            // Fallback for the (unusual) int.MaxValue upper bound.
            double sample = _random.NextDouble();
            long range = (long)maxInclusive - minInclusive + 1L;
            return (int)(minInclusive + (long)(sample * range));
        }
    }
}

/// <summary>
/// A deterministic <see cref="IRandomSource"/> that replays a fixed sequence of values.
/// Intended for tests and reproducible examples. When the sequence is exhausted it either
/// cycles from the start or repeats the final value.
/// </summary>
public sealed class SequenceRandomSource : IRandomSource
{
    private readonly int[] _values;
    private readonly bool _cycle;
    private int _index;

    /// <summary>Creates a source that cycles through <paramref name="values"/> forever.</summary>
    public SequenceRandomSource(params int[] values)
        : this(true, values)
    {
    }

    /// <summary>
    /// Creates a source over <paramref name="values"/>.
    /// When <paramref name="cycle"/> is <c>false</c> the last value is repeated once the sequence ends.
    /// </summary>
    public SequenceRandomSource(bool cycle, params int[] values)
    {
        if (values is null || values.Length == 0)
        {
            throw new ArgumentException("At least one value is required.", nameof(values));
        }

        _values = (int[])values.Clone();
        _cycle = cycle;
    }

    /// <inheritdoc />
    public int NextInt(int minInclusive, int maxInclusive)
    {
        int value;
        if (_index < _values.Length)
        {
            value = _values[_index];
            _index = _cycle ? (_index + 1) % _values.Length : Math.Min(_index + 1, _values.Length - 1);
        }
        else
        {
            value = _values[_values.Length - 1];
        }

        if (value < minInclusive || value > maxInclusive)
        {
            throw new InvalidOperationException(
                $"The scripted value {value} is outside the requested range {minInclusive}..{maxInclusive}.");
        }

        return value;
    }
}

/// <summary>A deterministic <see cref="IRandomSource"/> that always returns the same value.</summary>
public sealed class ConstantRandomSource : IRandomSource
{
    private readonly int _value;

    /// <summary>Creates a source that always returns <paramref name="value"/>.</summary>
    public ConstantRandomSource(int value) => _value = value;

    /// <inheritdoc />
    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (_value < minInclusive || _value > maxInclusive)
        {
            throw new InvalidOperationException(
                $"The fixed value {_value} is outside the requested range {minInclusive}..{maxInclusive}.");
        }

        return _value;
    }
}

/// <summary>
/// A deterministic <see cref="IRandomSource"/> that pulls values from a queue and falls back
/// to a fixed value once the queue is empty. Handy when a roll triggers extra dice (explosions,
/// rerolls, streak remedies) and you want those to be predictable.
/// </summary>
public sealed class QueueRandomSource : IRandomSource
{
    private readonly Queue<int> _values;
    private readonly int _fallback;

    /// <summary>Creates a source that yields <paramref name="values"/> in order, then <paramref name="fallback"/> forever.</summary>
    public QueueRandomSource(int fallback, params int[] values)
    {
        _fallback = fallback;
        _values = new Queue<int>(values ?? Array.Empty<int>());
    }

    /// <inheritdoc />
    public int NextInt(int minInclusive, int maxInclusive)
    {
        int value = _values.Count > 0 ? _values.Dequeue() : _fallback;
        if (value < minInclusive || value > maxInclusive)
        {
            throw new InvalidOperationException(
                $"The scripted value {value} is outside the requested range {minInclusive}..{maxInclusive}.");
        }

        return value;
    }
}
