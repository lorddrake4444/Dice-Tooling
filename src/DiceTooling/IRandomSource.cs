using System;

namespace DiceTooling;

/// <summary>
/// Abstraction over a source of random integers. Implement this to plug in a custom
/// random number generator (for example a cryptographic one, or a deterministic one
/// for tests).
/// </summary>
public interface IRandomSource
{
    /// <summary>
    /// Returns a uniformly distributed integer in the inclusive range
    /// <paramref name="minInclusive"/>..<paramref name="maxInclusive"/>.
    /// </summary>
    /// <param name="minInclusive">Lowest value that can be returned.</param>
    /// <param name="maxInclusive">Highest value that can be returned.</param>
    int NextInt(int minInclusive, int maxInclusive);
}
