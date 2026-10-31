using System.Globalization;
using System.Text;

namespace DiceTooling;

/// <summary>Builds a human-readable notation string from an explicit count/faces/options triple.</summary>
internal static class Notation
{
    public static string Describe(int count, int faces, RollOptions options)
    {
        var builder = new StringBuilder();
        builder.Append(count.ToString(CultureInfo.InvariantCulture));
        builder.Append('d');
        builder.Append(options.FateDie ? "F" : faces.ToString(CultureInfo.InvariantCulture));

        switch (options.KeepDrop)
        {
            case KeepDropRule.KeepHighest:
                builder.Append("kh").Append(options.KeepDropCount);
                break;
            case KeepDropRule.KeepLowest:
                builder.Append("kl").Append(options.KeepDropCount);
                break;
            case KeepDropRule.DropHighest:
                builder.Append("dh").Append(options.KeepDropCount);
                break;
            case KeepDropRule.DropLowest:
                builder.Append("dl").Append(options.KeepDropCount);
                break;
        }

        if (options.Reroll is { } reroll)
        {
            builder.Append(reroll.Once ? "r" : "rr").Append(reroll.Threshold);
        }

        if (options.Explode is { } explode)
        {
            builder.Append(explode.Penetrating ? "!!" : "!");
            if (explode.Threshold is int threshold)
            {
                builder.Append(">=").Append(threshold);
            }
        }

        if (options.MinimumValue is int minimum)
        {
            builder.Append("min").Append(minimum);
        }

        if (options.MaximumValue is int maximum)
        {
            builder.Append("max").Append(maximum);
        }

        if (options.TargetNumber is int target)
        {
            builder.Append(Comparisons.Symbol(options.SuccessComparison)).Append(target);
        }

        if (options.Modifier != 0)
        {
            builder.Append(options.Modifier > 0 ? "+" : string.Empty).Append(options.Modifier);
        }

        return builder.ToString();
    }
}
