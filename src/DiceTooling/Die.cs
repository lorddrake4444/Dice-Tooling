using System;
using System.Globalization;

namespace DiceTooling;

/// <summary>
/// A single die with a fixed number of faces. Immutable and comparable by face count.
/// </summary>
public readonly struct Die : IEquatable<Die>
{
    /// <summary>The smallest legal number of faces.</summary>
    public const int MinFaces = 2;

    /// <summary>The default number of faces (a d6).</summary>
    public const int DefaultFaces = 6;

    /// <summary>Creates a die with <paramref name="faces"/> faces (minimum <see cref="MinFaces"/>).</summary>
    public Die(int faces)
    {
        if (faces < MinFaces)
        {
            throw new ArgumentOutOfRangeException(
                nameof(faces),
                faces,
                $"A die must have at least {MinFaces} faces.");
        }

        Faces = faces;
    }

    /// <summary>The number of faces on the die.</summary>
    public int Faces { get; }

    /// <summary>Rolls the die once, returning a value in 1..<see cref="Faces"/>.</summary>
    public int Roll(IRandomSource random)
    {
        if (random is null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        return random.NextInt(1, Faces);
    }

    /// <summary>A four-sided die.</summary>
    public static Die D4 => new(4);

    /// <summary>A six-sided die.</summary>
    public static Die D6 => new(6);

    /// <summary>An eight-sided die.</summary>
    public static Die D8 => new(8);

    /// <summary>A ten-sided die.</summary>
    public static Die D10 => new(10);

    /// <summary>A twelve-sided die.</summary>
    public static Die D12 => new(12);

    /// <summary>A twenty-sided die.</summary>
    public static Die D20 => new(20);

    /// <summary>A hundred-sided die (percentile die).</summary>
    public static Die D100 => new(100);

    /// <summary>Parses text such as <c>"d20"</c>, <c>"20"</c> or <c>"d%"</c> into a die.</summary>
    public static Die Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Die text cannot be null or empty.", nameof(text));
        }

        string trimmed = text.Trim();
        if (trimmed.Length > 1 && (trimmed[0] == 'd' || trimmed[0] == 'D'))
        {
            trimmed = trimmed.Substring(1);
        }

        if (trimmed == "%")
        {
            return D100;
        }

        if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int faces))
        {
            throw new FormatException($"'{text}' is not a valid die.");
        }

        return new Die(faces);
    }

    /// <inheritdoc />
    public bool Equals(Die other) => Faces == other.Faces;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Die die && Equals(die);

    /// <inheritdoc />
    public override int GetHashCode() => Faces;

    /// <summary>Compares two dice by face count.</summary>
    public static bool operator ==(Die left, Die right) => left.Faces == right.Faces;

    /// <summary>Compares two dice by face count.</summary>
    public static bool operator !=(Die left, Die right) => left.Faces != right.Faces;

    /// <inheritdoc />
    public override string ToString() => "d" + Faces.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The result of a single die within a roll, together with any bookkeeping flags set
/// by the rolling pipeline (dropped, rerolled, exploded).
/// </summary>
/// <param name="Faces">The number of faces of the die that produced this value.</param>
/// <param name="Value">The value of the die.</param>
public sealed record DieRoll(int Faces, int Value)
{
    /// <summary>True when this die was dropped by a keep/drop rule.</summary>
    public bool IsDropped { get; init; }

    /// <summary>True when this die's value came from a reroll or a streak remedy.</summary>
    public bool IsRerolled { get; init; }

    /// <summary>True when this die was produced by an exploding-dice rule.</summary>
    public bool IsExploded { get; init; }

    /// <summary>Optional human-readable note (for example <c>"clamped"</c> or <c>"penetrating"</c>).</summary>
    public string? Note { get; init; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
