using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Chat_Triggers.Common;

/// <summary>
///     Evaluates the logic blocks of a chat trigger response, such as <c>{if(a==b):yes|no}</c> and
///     <c>{math:1+2}</c>.
/// </summary>
/// <remarks>
///     <para>
///         An instance holds the variables and queued commands of one trigger run, so a new one is needed per run. The
///         blocks are <c>{if(left op right):then|else}</c>, <c>{set(name):value}</c>, <c>{get(name)}</c>,
///         <c>{math:expr}</c>, <c>{upper:text}</c>, <c>{lower:text}</c>, <c>{replace(a,b):text}</c>,
///         <c>{choose:a|b|c}</c>, <c>{length:text}</c> and <c>{cmd:name args}</c>.
///     </para>
///     <para>
///         <c>{require(a,b):message}</c> and <c>{blacklist(a,b):message}</c> gate a run on the member's roles and
///         channel, and stop it with the message when the gate fails. <c>{override}</c> lets staff through both. The
///         gates only apply when a <see cref="TriggerGateContext" /> is supplied.
///     </para>
///     <para>
///         Anything that is not a well formed block is left as plain text. Nesting depth, evaluated block count and
///         output length are all capped, so a response cannot make the evaluator run away.
///     </para>
/// </remarks>
public sealed partial class TriggerBlockEvaluator
{
    /// <summary>
    ///     How deeply blocks may nest before the remainder is left as plain text.
    /// </summary>
    public const int MaxDepth = 8;

    /// <summary>
    ///     The longest output the evaluator will produce.
    /// </summary>
    public const int MaxOutputLength = 8000;

    /// <summary>
    ///     How many blocks one run may evaluate.
    /// </summary>
    public const int MaxBlocks = 300;

    /// <summary>
    ///     How many <c>{cmd:}</c> blocks one run may queue.
    /// </summary>
    public const int MaxCommands = 3;

    /// <summary>
    ///     How many roles and channels one <c>{require}</c> or <c>{blacklist}</c> block may list.
    /// </summary>
    public const int MaxGateItems = 25;

    private const int MaxVariables = 50;
    private const int MaxVariableLength = 1000;
    private const int MaxMathLength = 200;
    private const int MaxMathDepth = 32;

    private static readonly HashSet<string> ParenBlocks = new(StringComparer.Ordinal)
    {
        "if", "set", "get", "replace", "require", "blacklist"
    };

    private static readonly HashSet<string> BodyBlocks = new(StringComparer.Ordinal)
    {
        "if", "set", "math", "upper", "lower", "replace", "choose", "length", "cmd", "require", "blacklist"
    };

    private static readonly HashSet<string> PlainBlocks = new(StringComparer.Ordinal)
    {
        "math", "upper", "lower", "choose", "length", "cmd", "override"
    };

    private readonly List<string> commands = [];
    private readonly TriggerGateContext? gate;
    private readonly Random random;
    private readonly Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase);
    private int blocks;
    private string? gateMessage;
    private bool gateFailed;
    private bool overridden;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TriggerBlockEvaluator" /> class.
    /// </summary>
    /// <param name="random">The random source for <c>{choose:}</c>, or null to use the shared one.</param>
    /// <param name="gate">The member and channel <c>{require}</c> and <c>{blacklist}</c> check, or null to skip them.</param>
    public TriggerBlockEvaluator(Random? random = null, TriggerGateContext? gate = null)
    {
        this.random = random ?? Random.Shared;
        this.gate = gate;
    }

    /// <summary>
    ///     Gets a value indicating whether a <c>{require}</c> or <c>{blacklist}</c> block stopped the run.
    /// </summary>
    public bool Stopped { get; private set; }

    /// <summary>
    ///     Gets the commands queued by <c>{cmd:}</c> blocks, without the prefix.
    /// </summary>
    public IReadOnlyList<string> Commands
    {
        get
        {
            return commands;
        }
    }

    /// <summary>
    ///     Evaluates every block in the input.
    /// </summary>
    /// <param name="input">The response text, with placeholders already replaced.</param>
    /// <returns>
    ///     The text with each well formed block replaced by its result, or the failing gate's message when
    ///     <see cref="Stopped" /> is set.
    /// </returns>
    [return: NotNullIfNotNull(nameof(input))]
    public string? Evaluate(string? input)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('{'))
            return input;

        var output = Run(input, 0);
        if (!gateFailed || overridden && gate!.IsStaff)
            return output;

        Stopped = true;
        commands.Clear();
        return gateMessage ?? "";
    }

    private string Run(string text, int depth)
    {
        if (depth > MaxDepth || !text.Contains('{'))
            return Clamp(text);

        var sb = new StringBuilder();
        var i = 0;

        while (i < text.Length && sb.Length < MaxOutputLength)
        {
            if (text[i] == '{' && TryParseBlock(text, i, out var block))
            {
                sb.Append(++blocks <= MaxBlocks ? Execute(block, depth) : block.Raw);
                i = block.End;
                continue;
            }

            sb.Append(text[i]);
            i++;
        }

        return Clamp(sb.ToString());
    }

    private static string Clamp(string text)
    {
        return text.Length > MaxOutputLength ? text[..MaxOutputLength] : text;
    }

    private static bool TryParseBlock(string text, int start, out Block block)
    {
        block = default;
        var pos = start + 1;

        while (pos < text.Length && text[pos] is >= 'a' and <= 'z')
            pos++;

        var name = text[(start + 1)..pos];
        if (name.Length == 0 || !(ParenBlocks.Contains(name) || PlainBlocks.Contains(name)))
            return false;

        string? args = null;
        if (pos < text.Length && text[pos] == '(')
        {
            if (!ParenBlocks.Contains(name))
                return false;

            var close = FindClosing(text, pos, '(', ')');
            if (close < 0)
                return false;

            args = text[(pos + 1)..close];
            pos = close + 1;
        }
        else if (ParenBlocks.Contains(name))
        {
            return false;
        }

        if (pos >= text.Length)
            return false;

        string? body = null;
        if (text[pos] == ':' && BodyBlocks.Contains(name))
        {
            var close = FindClosing(text, start, '{', '}');
            if (close < 0 || close < pos)
                return false;

            body = text[(pos + 1)..close];
            pos = close + 1;
        }
        else if (text[pos] == '}' && name is "get" or "override")
        {
            pos++;
        }
        else
        {
            return false;
        }

        block = new Block(name, args, body, pos, text[start..pos]);
        return true;
    }

    private static int FindClosing(string text, int open, char openChar, char closeChar)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == openChar)
            {
                depth++;
            }
            else if (text[i] == closeChar && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private string Execute(Block block, int depth)
    {
        var next = depth + 1;
        if (next > MaxDepth)
            return block.Raw;

        switch (block.Name)
        {
            case "if":
                return ExecuteIf(block, next);
            case "set":
            {
                var name = block.Args!.Trim();
                if (!VariableName().IsMatch(name))
                    return block.Raw;

                var value = Run(block.Body!, next);
                if (variables.ContainsKey(name) || variables.Count < MaxVariables)
                    variables[name] = value.Length > MaxVariableLength ? value[..MaxVariableLength] : value;
                return "";
            }
            case "get":
                return variables.GetValueOrDefault(block.Args!.Trim()) ?? "";
            case "math":
            {
                var result = EvaluateMath(Run(block.Body!, next));
                return result ?? block.Raw;
            }
            case "upper":
                return Run(block.Body!, next).ToUpperInvariant();
            case "lower":
                return Run(block.Body!, next).ToLowerInvariant();
            case "length":
                return Run(block.Body!, next).Length.ToString(CultureInfo.InvariantCulture);
            case "choose":
            {
                var options = SplitTopLevel(block.Body!, '|', int.MaxValue);
                return Run(options[random.Next(options.Count)], next);
            }
            case "replace":
            {
                var parts = SplitTopLevel(block.Args!, ',', 2);
                if (parts.Count != 2)
                    return block.Raw;

                var find = Run(parts[0], next);
                var with = Run(parts[1], next);
                var text = Run(block.Body!, next);
                if (find.Length == 0)
                    return text;

                var worst = (long)text.Length / find.Length * Math.Max(0, with.Length - find.Length) + text.Length;
                return worst > MaxOutputLength ? block.Raw : text.Replace(find, with, StringComparison.Ordinal);
            }
            case "cmd":
            {
                var command = Run(block.Body!, next).Trim();
                if (command.Length == 0)
                    return block.Raw;

                if (commands.Count < MaxCommands)
                    commands.Add(command);
                return "";
            }
            case "override":
                overridden = true;
                return "";
            case "require":
            case "blacklist":
            {
                var items = block.Args!
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Take(MaxGateItems)
                    .ToList();
                if (items.Count == 0)
                    return block.Raw;

                var message = Run(block.Body!, next);
                if (gate is not null && !gateFailed && !PassesGate(items, block.Name == "require"))
                {
                    gateFailed = true;
                    gateMessage = message;
                }

                return "";
            }
            default:
                return block.Raw;
        }
    }

    private bool PassesGate(List<string> items, bool required)
    {
        var inListedChannel = false;
        var hasChannels = false;

        foreach (var item in items)
        {
            if (!gate!.TryResolve(item, out var isRole, out var id))
            {
                if (required)
                    return false;

                continue;
            }

            if (isRole)
            {
                var holds = gate.MemberRoleIds.Contains(id);
                if (required != holds)
                    return false;

                continue;
            }

            hasChannels = true;
            inListedChannel |= gate.ChannelIds.Contains(id);
        }

        return !hasChannels || inListedChannel == required;
    }

    private string ExecuteIf(Block block, int depth)
    {
        var cond = block.Args!;
        if (!TryFindOperator(cond, out var index, out var op, out var length))
            return block.Raw;

        var left = Run(cond[..index].Trim(), depth);
        var right = Run(cond[(index + length)..].Trim(), depth);

        var branches = SplitTopLevel(block.Body!, '|', 2);
        var chosen = Compare(left, right, op) ? branches[0] : branches.Count > 1 ? branches[1] : "";
        return Run(chosen, depth);
    }

    private static bool TryFindOperator(string cond, out int index, out string op, out int length)
    {
        var braces = 0;
        for (var i = 0; i < cond.Length; i++)
        {
            var c = cond[i];
            if (c == '{')
            {
                braces++;
                continue;
            }

            if (c == '}')
            {
                braces--;
                continue;
            }

            if (braces != 0)
                continue;

            if (i + 1 < cond.Length && cond[i + 1] == '=' && c is '=' or '!' or '>' or '<')
            {
                index = i;
                op = cond.Substring(i, 2);
                length = 2;
                return true;
            }

            if (c is '>' or '<')
            {
                index = i;
                op = c.ToString();
                length = 1;
                return true;
            }

            if (c == ' ' && string.Compare(cond, i, " contains ", 0, 10, StringComparison.OrdinalIgnoreCase) == 0)
            {
                index = i;
                op = "contains";
                length = 10;
                return true;
            }
        }

        index = 0;
        op = "";
        length = 0;
        return false;
    }

    private static bool Compare(string left, string right, string op)
    {
        if (op == "contains")
            return left.Contains(right, StringComparison.OrdinalIgnoreCase);

        int order;
        if (decimal.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
            && decimal.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
        {
            order = a.CompareTo(b);
        }
        else
        {
            order = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        return op switch
        {
            "==" => order == 0,
            "!=" => order != 0,
            ">" => order > 0,
            "<" => order < 0,
            ">=" => order >= 0,
            "<=" => order <= 0,
            _ => false
        };
    }

    private static List<string> SplitTopLevel(string text, char separator, int max)
    {
        var parts = new List<string>();
        var braces = 0;
        var start = 0;

        for (var i = 0; i < text.Length && parts.Count < max - 1; i++)
        {
            switch (text[i])
            {
                case '{':
                    braces++;
                    break;
                case '}':
                    braces--;
                    break;
            }

            if (text[i] != separator || braces != 0)
                continue;

            parts.Add(text[start..i]);
            start = i + 1;
        }

        parts.Add(text[start..]);
        return parts;
    }

    /// <summary>
    ///     Evaluates an arithmetic expression of numbers, <c>+ - * / %</c> and parentheses.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns>The result as text, or null when the expression is invalid or cannot be computed.</returns>
    public static string? EvaluateMath(string expression)
    {
        expression = expression.Trim();
        if (expression.Length == 0 || expression.Length > MaxMathLength)
            return null;

        try
        {
            var pos = 0;
            var value = ParseSum(expression, ref pos, 0);
            SkipSpaces(expression, ref pos);
            if (pos != expression.Length)
                return null;

            return value.ToString("0.##########", CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or OverflowException or DivideByZeroException)
        {
            return null;
        }
    }

    private static decimal ParseSum(string s, ref int pos, int depth)
    {
        var value = ParseProduct(s, ref pos, depth);
        while (true)
        {
            SkipSpaces(s, ref pos);
            if (pos >= s.Length || s[pos] is not ('+' or '-'))
                return value;

            var op = s[pos++];
            var rhs = ParseProduct(s, ref pos, depth);
            value = op == '+' ? value + rhs : value - rhs;
        }
    }

    private static decimal ParseProduct(string s, ref int pos, int depth)
    {
        var value = ParseFactor(s, ref pos, depth);
        while (true)
        {
            SkipSpaces(s, ref pos);
            if (pos >= s.Length || s[pos] is not ('*' or '/' or '%'))
                return value;

            var op = s[pos++];
            var rhs = ParseFactor(s, ref pos, depth);
            value = op switch
            {
                '*' => value * rhs,
                '/' => value / rhs,
                _ => value % rhs
            };
        }
    }

    private static decimal ParseFactor(string s, ref int pos, int depth)
    {
        if (depth > MaxMathDepth)
            throw new FormatException();

        SkipSpaces(s, ref pos);
        if (pos >= s.Length)
            throw new FormatException();

        switch (s[pos])
        {
            case '-':
                pos++;
                return -ParseFactor(s, ref pos, depth + 1);
            case '+':
                pos++;
                return ParseFactor(s, ref pos, depth + 1);
            case '(':
            {
                pos++;
                var value = ParseSum(s, ref pos, depth + 1);
                SkipSpaces(s, ref pos);
                if (pos >= s.Length || s[pos] != ')')
                    throw new FormatException();

                pos++;
                return value;
            }
        }

        var start = pos;
        while (pos < s.Length && (char.IsAsciiDigit(s[pos]) || s[pos] == '.'))
            pos++;

        if (pos == start)
            throw new FormatException();

        return decimal.Parse(s.AsSpan(start, pos - start), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);
    }

    private static void SkipSpaces(string s, ref int pos)
    {
        while (pos < s.Length && s[pos] == ' ')
            pos++;
    }

    [GeneratedRegex(@"^[\w-]{1,32}$")]
    private static partial Regex VariableName();

    private readonly record struct Block(string Name, string? Args, string? Body, int End, string Raw);
}
