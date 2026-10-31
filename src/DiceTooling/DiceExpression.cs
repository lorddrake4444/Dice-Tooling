using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DiceTooling;

/// <summary>
/// A parsed dice expression such as <c>4d6kh3+2</c>, <c>d20</c>, <c>6d10&gt;=8</c> or <c>2d20kl1</c>.
/// </summary>
public sealed class DiceExpression
{
    private static readonly Regex LeadRegex =
        new(@"^(?<count>\d+)?[dD](?<faces>\d+|%|[fF])", RegexOptions.Compiled);

    private static readonly Regex KeepDropRegex =
        new(@"^(?<kind>kh|kl|dh|dl)(?<n>\d+)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExplodeRegex =
        new(@"^(?<kind>!!|!)(?:(?<op>=|>=|>)?(?<n>\d+))?", RegexOptions.Compiled);

    private static readonly Regex RerollRegex =
        new(@"^(?<kind>rr|ro|r)(?<n>\d+)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MinMaxRegex =
        new(@"^(?<kind>min|max)(?<n>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TargetRegex =
        new(@"^(?<op>>=|<=|>|<|=)(?<n>\d+)", RegexOptions.Compiled);

    private static readonly Regex ModifierRegex =
        new(@"^(?<sign>[+-])(?<n>\d+)", RegexOptions.Compiled);

    private DiceExpression(int count, int faces, RollOptions options, string source)
    {
        Count = count;
        Faces = faces;
        Options = options;
        Source = source;
    }

    /// <summary>The number of dice.</summary>
    public int Count { get; }

    /// <summary>The number of faces.</summary>
    public int Faces { get; }

    /// <summary>The options decoded from the notation.</summary>
    public RollOptions Options { get; }

    /// <summary>The original notation text.</summary>
    public string Source { get; }

    /// <summary>
    /// Parses dice notation. Supported tokens, in any order after the leading <c>NdM</c>:
    /// <list type="bullet">
    ///   <item><description><c>khN</c> / <c>klN</c> / <c>dhN</c> / <c>dlN</c> — keep/drop highest/lowest N (N defaults to 1).</description></item>
    ///   <item><description><c>rN</c> / <c>roN</c> — reroll values up to N once; <c>rrN</c> rerolls repeatedly.</description></item>
    ///   <item><description><c>!</c> — explode on max; <c>!&gt;=N</c> / <c>!&gt;N</c> / <c>!=N</c> explode on a threshold; <c>!!</c> penetrating.</description></item>
    ///   <item><description><c>minN</c> / <c>maxN</c> — clamp dice to a floor/ceiling.</description></item>
    ///   <item><description><c>&gt;=N</c> <c>&gt;N</c> <c>&lt;=N</c> <c>&lt;N</c> <c>=N</c> — count successes instead of summing.</description></item>
    ///   <item><description><c>+N</c> / <c>-N</c> — flat modifier.</description></item>
    /// </list>
    /// <c>dF</c> produces a Fudge/Fate die and <c>d%</c> is a d100.
    /// </summary>
    public static DiceExpression Parse(string notation)
    {
        if (string.IsNullOrWhiteSpace(notation))
        {
            throw new ArgumentException("Dice notation cannot be null or empty.", nameof(notation));
        }

        string source = notation.Trim();
        Match lead = LeadRegex.Match(source);
        if (!lead.Success)
        {
            throw new FormatException($"Cannot parse '{source}': expected a dice expression such as '4d6', 'd20+3' or '4d6kh3'.");
        }

        int count = lead.Groups["count"].Success
            ? int.Parse(lead.Groups["count"].Value, CultureInfo.InvariantCulture)
            : 1;

        string faceToken = lead.Groups["faces"].Value;
        bool fate = faceToken is "f" or "F";
        int faces = fate ? 3 : faceToken == "%" ? 100 : int.Parse(faceToken, CultureInfo.InvariantCulture);

        int position = lead.Length;
        RollOptions options = RollOptions.Default with { FateDie = fate };

        while (position < source.Length)
        {
            if (char.IsWhiteSpace(source[position]))
            {
                position++;
                continue;
            }

            string rest = source.Substring(position);
            Match match;

            if ((match = KeepDropRegex.Match(rest)).Success)
            {
                int n = match.Groups["n"].Success
                    ? int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture)
                    : 1;
                string kind = match.Groups["kind"].Value.ToLowerInvariant();
                options = options with
                {
                    KeepDrop = kind switch
                    {
                        "kh" => KeepDropRule.KeepHighest,
                        "kl" => KeepDropRule.KeepLowest,
                        "dh" => KeepDropRule.DropHighest,
                        _ => KeepDropRule.DropLowest,
                    },
                    KeepDropCount = n,
                };
                position += match.Length;
                continue;
            }

            if ((match = ExplodeRegex.Match(rest)).Success)
            {
                bool penetrating = match.Groups["kind"].Value == "!!";
                int? threshold = null;
                if (match.Groups["n"].Success)
                {
                    int n = int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
                    threshold = match.Groups["op"].Value == ">" ? n + 1 : n;
                }

                options = options with { Explode = new ExplodeRule(threshold, penetrating) };
                position += match.Length;
                continue;
            }

            if ((match = RerollRegex.Match(rest)).Success)
            {
                int n = match.Groups["n"].Success
                    ? int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture)
                    : 1;
                bool repeat = match.Groups["kind"].Value.ToLowerInvariant() == "rr";
                options = options with { Reroll = new RerollRule(n, Once: !repeat) };
                position += match.Length;
                continue;
            }

            if ((match = MinMaxRegex.Match(rest)).Success)
            {
                int n = int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
                options = match.Groups["kind"].Value.ToLowerInvariant() == "min"
                    ? options with { MinimumValue = n }
                    : options with { MaximumValue = n };
                position += match.Length;
                continue;
            }

            if ((match = TargetRegex.Match(rest)).Success)
            {
                int n = int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
                options = options with
                {
                    TargetNumber = n,
                    SuccessComparison = match.Groups["op"].Value switch
                    {
                        ">=" => Comparison.AtLeast,
                        "<=" => Comparison.AtMost,
                        ">" => Comparison.GreaterThan,
                        "<" => Comparison.LessThan,
                        _ => Comparison.Equal,
                    },
                };
                position += match.Length;
                continue;
            }

            if ((match = ModifierRegex.Match(rest)).Success)
            {
                int n = int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
                int signed = match.Groups["sign"].Value == "-" ? -n : n;
                options = options with { Modifier = options.Modifier + signed };
                position += match.Length;
                continue;
            }

            throw new FormatException($"Cannot parse '{source}': unexpected token '{rest}' at position {position}.");
        }

        return new DiceExpression(count, faces, options, source);
    }

    /// <summary>Rolls the expression using the supplied random source (or the shared default).</summary>
    public RollResult Roll(IRandomSource? random = null, StreakGuard? streakGuard = null) =>
        DiceRoller.Roll(Count, Faces, Options, Source, random ?? Dice.Random, streakGuard ?? Dice.StreakGuard);

    /// <summary>Parses and rolls in one step.</summary>
    public static RollResult Roll(string notation, IRandomSource? random = null, StreakGuard? streakGuard = null) =>
        Parse(notation).Roll(random, streakGuard);

    /// <inheritdoc />
    public override string ToString() => Source;
}
