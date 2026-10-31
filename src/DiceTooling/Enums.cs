namespace DiceTooling;

/// <summary>How a pool of dice is trimmed before it is totalled.</summary>
public enum KeepDropRule
{
    /// <summary>Keep every die.</summary>
    None = 0,

    /// <summary>Keep only the N highest dice (for example <c>4d6kh3</c>).</summary>
    KeepHighest,

    /// <summary>Keep only the N lowest dice.</summary>
    KeepLowest,

    /// <summary>Remove the N highest dice.</summary>
    DropHighest,

    /// <summary>Remove the N lowest dice.</summary>
    DropLowest,
}

/// <summary>A comparison used when counting successes in a dice pool.</summary>
public enum Comparison
{
    /// <summary>value &gt;= target.</summary>
    AtLeast = 0,

    /// <summary>value &lt;= target.</summary>
    AtMost,

    /// <summary>value &gt; target.</summary>
    GreaterThan,

    /// <summary>value &lt; target.</summary>
    LessThan,

    /// <summary>value == target.</summary>
    Equal,

    /// <summary>value != target.</summary>
    NotEqual,
}

/// <summary>Classifies a roll as unusually low, typical, or unusually high.</summary>
public enum RollQuality
{
    /// <summary>The roll was below average.</summary>
    Low = 0,

    /// <summary>The roll was around average.</summary>
    Normal,

    /// <summary>The roll was above average.</summary>
    High,
}

/// <summary>What a streak guard does when it detects a streak.</summary>
public enum StreakRemedy
{
    /// <summary>Detect the streak but change nothing.</summary>
    None = 0,

    /// <summary>Reroll the single worst kept die, keeping whichever of the two values favours an average roll.</summary>
    RerollWorstDie,

    /// <summary>Reroll the single best kept die, keeping whichever of the two values favours an average roll.</summary>
    RerollBestDie,

    /// <summary>Reroll every kept die and keep the value that pulls the roll back towards average.</summary>
    RerollEntirePool,
}
