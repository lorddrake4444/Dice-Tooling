namespace DiceTooling;

/// <summary>
/// Describes when extra dice are added because a die "exploded" (also called an open roll).
/// </summary>
/// <param name="Threshold">
/// A die explodes when its value is greater than or equal to this number. When <c>null</c>
/// a die explodes only when it rolls its maximum face.
/// </param>
/// <param name="Penetrating">
/// When true, exploding dice use the "penetrating" variant: each extra die contributes
/// <c>value - 1</c> (minimum 1) instead of its full value.
/// </param>
/// <param name="MaxDepth">Safety cap on the explosion chain length per die.</param>
/// <param name="MaxExplosions">Safety cap on the total number of extra dice produced.</param>
public sealed record ExplodeRule(int? Threshold = null, bool Penetrating = false, int MaxDepth = 100, int MaxExplosions = 1000)
{
    /// <summary>Explode on the maximum face only (notation <c>4d6!</c>).</summary>
    public static ExplodeRule OnMax { get; } = new();

    /// <summary>Explode on any value at or above <paramref name="threshold"/> (notation <c>4d6!&gt;=5</c>).</summary>
    public static ExplodeRule AtLeast(int threshold) => new(threshold);

    /// <summary>True when a die showing <paramref name="value"/> should explode.</summary>
    public bool ShouldExplode(int value, int faces) => value >= (Threshold ?? faces);
}

/// <summary>
/// Describes when a die is rerolled because it was too low (for example "reroll ones").
/// </summary>
/// <param name="Threshold">A die is rerolled when its value is less than or equal to this number.</param>
/// <param name="Once">When true the die is rerolled a single time; when false it is rerolled until it beats the threshold.</param>
/// <param name="DropInsteadOfReroll">When true a die that is still at or below the threshold after rerolling is dropped.</param>
/// <param name="MaxRerolls">Safety cap on the total number of rerolls in a single pool.</param>
public sealed record RerollRule(int Threshold, bool Once = true, bool DropInsteadOfReroll = false, int MaxRerolls = 100)
{
    /// <summary>Reroll ones once (notation <c>4d6r1</c>).</summary>
    public static RerollRule Ones(bool once = true) => new(1, once);

    /// <summary>True when a die showing <paramref name="value"/> should be rerolled.</summary>
    public bool ShouldReroll(int value) => value <= Threshold;
}
