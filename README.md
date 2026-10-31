# DiceTooling

A small, dependency-free dice engine for tabletop RPGs, targeting **C# 10 / .NET 6**.
Everything lives in the `DiceTooling` namespace in one assembly, with no third-party packages.

## Layout

```
src/DiceTooling/            the library
tests/DiceTooling.Tests/    xUnit tests
samples/DiceTooling.Samples/ runnable tour of every feature
```

Build, test and run:

```powershell
dotnet build
dotnet test
dotnet run --project samples/DiceTooling.Samples
```

## Quick start

```csharp
using DiceTooling;

// One call, any count, any faces.
RollResult a = Dice.Roll(4, 6);        // 4d6
RollResult b = Dice.Roll(1, 20);       // d20
RollResult c = Dice.Roll(3, 100);      // 3d100

// Or use notation.
RollResult d = Dice.Roll("4d6kh3+2");  // 4d6 keep highest 3, plus 2
Console.WriteLine(d);                  // 4d6kh3+2 = 15 [6, 5, 4 | dropped: 2]
```

## Features

- **Any pool in one call** — `Dice.Roll(count, faces, options)` or `Dice.Roll("NdM…")`.
- **Keep / drop, any N** — `khN`, `klN`, `dhN`, `dlN` (keep/drop highest/lowest).
- **Modifiers & clamps** — flat `+N`/`-N`, `minN`, `maxN`.
- **Rerolls** — `rN`/`roN` (reroll once), `rrN` (reroll repeatedly), optionally dropping the result.
- **Exploding dice** — `!` on max, `!>=N` / `!>N` / `!=N` by threshold, `!!` penetrating.
- **Success pools** — `>=N`, `>N`, `<=N`, `<N`, `=N` count successes instead of summing.
- **Fudge/Fate dice** — `4dF` yields values -1, 0, +1.
- **Percentile** — `d%` is a d100.
- **Advantage / disadvantage** — `Dice.D20(advantage: true)` and friends.
- **Bad/good streak prevention** — an opt-in, configurable safety net (see below).
- **Injectable randomness** — `IRandomSource`; `SequenceRandomSource`, `ConstantRandomSource`,
  `QueueRandomSource` and seeded `RandomSource` for deterministic tests.
- **Statistics** — exact distributions via `DiceStatistics.SumOfDice`, plus Monte Carlo estimates.

## Notation reference

| Token | Meaning | Example |
| --- | --- | --- |
| `NdM` | N dice of M faces (N defaults to 1) | `4d6`, `d20` |
| `d%` | percentile die (100 faces) | `d%` |
| `dF` | Fudge/Fate die (-1, 0, +1) | `4dF` |
| `khN` / `klN` | keep N highest / lowest | `4d6kh3` |
| `dhN` / `dlN` | drop N highest / lowest | `5d10dl2` |
| `rN` / `roN` | reroll values ≤ N once | `4d6r1` |
| `rrN` | reroll values ≤ N repeatedly | `4d6rr2` |
| `!` | explode on max face | `4d6!` |
| `!>=N` / `!>N` / `!=N` | explode on a threshold | `4d6!>=5` |
| `!!` | penetrating explosion | `4d6!!` |
| `minN` / `maxN` | clamp dice | `4d6min2` |
| `>=N` `>N` `<=N` `<N` `=N` | count successes | `6d10>=8` |
| `+N` / `-N` | flat modifier | `d20+7` |

Tokens can be combined and appear in any order, for example `10d6!>=6dl1+4`.

## Streak prevention

Streak prevention is **off unless you ask for it**. A roll is classified by comparing the average of
its kept dice against the die midpoint `(faces + 1) / 2`:

- average below `midpoint * (1 - Tolerance)` → a **low** roll (builds a bad streak),
- average above `midpoint * (1 + Tolerance)` → a **high** roll (builds a good streak),
- anything in between resets both streaks.

Once a streak reaches its length, the matching remedy fires and the streak resets. This deliberately
bends the probability curve, which is why it is opt-in.

```csharp
var session = DiceSession.WithSeed(1234);

var options = RollOptions.Default with
{
    StreakPrevention = new StreakPreventionOptions
    {
        StreakLength      = 3,                              // three in a row before intervening
        Tolerance         = 0.1,                            // +/-10% band counts as "average"
        BadStreakRemedy   = StreakRemedy.RerollWorstDie,    // mercy: reroll the worst die, keep the better value
        GoodStreakRemedy  = StreakRemedy.RerollBestDie,     // pity:  reroll the best die, keep the worse value
        MaxRemediesPerRoll = 1,
    },
};

RollResult r = session.Roll(4, 6, options);
foreach (StreakAdjustment change in r.Adjustments)
{
    Console.WriteLine(change); // die #2: 1 -> 6 (bad-streak mercy)
}

// Inspect the tracked history at any time.
StreakSnapshot? snapshot = session.GetStreakSnapshot("4d6");
```

Remedies: `None`, `RerollWorstDie`, `RerollBestDie`, `RerollEntirePool`.
History is kept per expression string, so `3d6` and `1d20` never interfere. The static `Dice`
helpers share a process-wide `Dice.StreakGuard`; a `DiceSession` owns its own.

## Sessions, seeding and custom randomness

```csharp
// Reproducible: same seed, same rolls.
var session = DiceSession.WithSeed(seed: 42);

// Plug in your own generator.
public sealed class MyRandom : IRandomSource
{
    public int NextInt(int minInclusive, int maxInclusive) => /* ... */;
}

Dice.Random = new MyRandom();          // affects the static helpers
var custom = new DiceSession(new MyRandom());
```

## Results

`RollResult` keeps everything the pipeline did:

- `Total` — kept sum plus modifier (or successes plus modifier in a pool),
- `KeptSum`, `Successes`, `Modifier`, `IsEmpty`,
- `Kept` / `Dropped` / `Rolls` with per-die flags `IsDropped`, `IsRerolled`, `IsExploded`, `Note`,
- `Adjustments` for streak remedies,
- `IsAllMaximum()` / `IsAllOnes()` helpers, `MeetsAtLeast(int)`,
- `ToString()` for a compact line, `Describe()` for a full breakdown.

## Odds

```csharp
Distribution d6 = DiceStatistics.SumOfDice(1, 6);
Console.WriteLine(d6.Mean);                     // 3.5
Console.WriteLine(d6.ProbabilityOf(6));         // 0.1666...
Console.WriteLine(DiceStatistics.ToTextHistogram(d6));

RollSummary keepThree = DiceStatistics.MonteCarlo("4d6kh3", iterations: 200_000, seed: 1234);
Console.WriteLine(keepThree);                   // mean ~12.24
```

`SumOfDice` is exact (dynamic programming) for plain pools; `MonteCarlo` estimates anything,
including keep/drop, explosions and streak prevention.

## Design notes

The public pipeline runs in one fixed order: **roll → reroll → explode → clamp → keep/drop →
streak prevention → total**. Every feature is expressed through the immutable `RollOptions` record,
so variations are made with `with`:

```csharp
RollOptions options = RollOptions.KeepHighest(3) with
{
    Modifier = 2,
    StreakPrevention = new StreakPreventionOptions { StreakLength = 4 },
};
RollResult result = Dice.Roll(4, 6, options);
```
