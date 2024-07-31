using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Service for parsing and processing dice roll commands.
/// </summary>
public partial class RollCommandService : INService
{
    private static readonly Regex Cleaner = MyRegex1();

    private static readonly Regex DieFinder = MyRegex2();

    private static readonly Regex OperationFinder =
        MyRegex();


    /// <summary>
    /// Parses a dice roll command string into individual rolls and modifiers, then executes the rolls.
    /// </summary>
    /// <param name="roll">The command string representing the dice roll.</param>
    /// <returns>A RollResult object containing the outcomes of the rolls and total result.</returns>
    public static RollResult ParseRoll(string roll)
    {
        var parsed = Cleaner.Replace(roll, "1d$1$2");

        var dies = DieFinder.Matches(parsed)
            .Select(x => new Die(int.TryParse(x.Groups["count"].Value, out var c) ? c : 1,
                int.TryParse(x.Groups["value"].Value, out var s)
                    ? s
                    : throw new ArgumentException("roll_fail_invalid_string")))
            .ToList();
        if (dies.Any(x => x.Sides is >= int.MaxValue or < 0))
            throw new ArgumentException("roll_fail_dice_sides");
        if (dies.Count == 0)
            throw new ArgumentException("roll_fail_no_dice");

        var opResult = OperationFinder.Match(parsed);

        RollResult result = new();
        try
        {
            // throw errors on int overflow.
            checked
            {
                Random random = new();
                foreach (var d in dies)
                {
                    for (var i = 0; i < d.Count; i++)
                    {
                        var value = random.Next(d.Sides) + 1;
                        var dict = result.Results.GetValueOrDefault(d, []);
                        dict.Add(value);
                        result.Results[d] = dict;
                        result.Total += value;
                    }
                }

                if (opResult.Success)
                {
                    var op = opResult.Groups["operator"].Value.First();
                    var opVal = int.Parse(opResult.Groups["number"].Value);
                    switch (op)
                    {
                        case '+':
                        {
                            result.Total += opVal;
                        }
                            break;
                        case '/':
                        case '\\':
                        {
                            result.Total /= opVal;
                        }
                            break;
                        case '*':
                        {
                            result.Total *= opVal;
                        }
                            break;
                        case '-':
                        {
                            result.Total -= opVal;
                        }
                            break;
                        default:
                            throw new NotSupportedException("unknown operation.");
                    }
                }
            }
        }
        // mark int overflow errors.
        // this still allows for individual die results, but won't produce an inaccurate total.
        catch (OverflowException)
        {
            result.InacurateTotal = true;
        }

        return result;
    }

    [GeneratedRegex(@"(?'operator'[\/\\+\-*]) *?(?'number'\d*)$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"[^\d]d(\d*)|^d(\d*)", RegexOptions.Compiled)]
    private static partial Regex MyRegex1();

    [GeneratedRegex(@"(?'count'\d+)?d(?'value'\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex2();
}