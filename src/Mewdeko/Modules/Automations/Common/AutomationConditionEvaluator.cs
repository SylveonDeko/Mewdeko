using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Automations.Common;

/// <summary>
///     Checks the conditions of an automation against an event.
/// </summary>
public static class AutomationConditionEvaluator
{
    private const int MaxCachedPatterns = 500;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly ConcurrentDictionary<string, Regex?> Patterns = new();

    /// <summary>
    ///     Finds the first condition that does not pass.
    /// </summary>
    /// <param name="conditions">The conditions. All of them have to pass.</param>
    /// <param name="data">The event.</param>
    /// <returns>The position of the first failing condition, or -1 when all of them pass.</returns>
    public static int FirstFailure(IReadOnlyList<AutomationNode> conditions, AutomationEventData data)
    {
        for (var i = 0; i < conditions.Count; i++)
        {
            if (!Passes(conditions[i], data))
                return i;
        }

        return -1;
    }

    /// <summary>
    ///     Checks one condition. A condition that needs information the event does not have does not pass.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="data">The event.</param>
    public static bool Passes(AutomationNode condition, AutomationEventData data)
    {
        switch (condition.Type.ToLowerInvariant())
        {
            case "inchannels":
            {
                var ids = condition.GetIds("channelIds");
                return ids.Contains(data.ChannelId) ||
                       data.ParentChannelId != 0 && ids.Contains(data.ParentChannelId) ||
                       data.CategoryId != 0 && ids.Contains(data.CategoryId);
            }
            case "hasanyrole":
                return condition.GetIds("roleIds").Any(data.RoleIds.Contains);
            case "hasallroles":
                return condition.GetIds("roleIds").All(data.RoleIds.Contains);
            case "hasnoneofroles":
                return !condition.GetIds("roleIds").Any(data.RoleIds.Contains);
            case "lacksanyrole":
                return condition.GetIds("roleIds").Any(x => !data.RoleIds.Contains(x));
            case "fromusers":
                return condition.GetIds("userIds").Contains(data.UserId);
            case "messagecontains":
                return data.MessageKnown && data.Content is { } contains &&
                       contains.Contains(condition.GetString("text") ?? "", Comparison(condition));
            case "messageequals":
                return data.MessageKnown && data.Content is { } equals &&
                       equals.Trim().Equals(condition.GetString("text")?.Trim() ?? "", Comparison(condition));
            case "messagedoesnotcontain":
                return data.MessageKnown && !(data.Content ?? "")
                    .Contains(condition.GetString("text") ?? "", Comparison(condition));
            case "messagematchesregex":
                return data.MessageKnown && MatchesRegex(condition.GetString("pattern"), data.Content ?? "");
            case "hasattachment":
                return data.MessageKnown && condition.GetStrings("types")
                    .Any(x => data.AttachmentTypes.Contains(x, StringComparer.OrdinalIgnoreCase));
            case "hastext":
                return data.MessageKnown && !string.IsNullOrWhiteSpace(data.Content);
            case "isreply":
                return data.MessageKnown && data.IsReply;
            case "isnotreply":
                return data.MessageKnown && !data.IsReply;
            case "reactionemojiin":
            {
                var keys = condition.GetStrings("emojis").Select(AutomationEmoji.Normalize);
                return data.EmojiKey is not null && keys.Contains(data.EmojiKey);
            }
            case "reactioncount":
            {
                var count = condition.GetLong("count");
                return (condition.GetString("comparison") ?? "").ToLowerInvariant() switch
                {
                    "greaterorequal" => data.ReactionCount >= count,
                    "lessorequal" => data.ReactionCount <= count,
                    "equal" => data.ReactionCount == count,
                    _ => false
                };
            }
            case "buttoncustomidis":
                return data.ButtonCustomId is not null &&
                       data.ButtonCustomId.Equals(condition.GetString("customId"), StringComparison.Ordinal);
            case "triggerroleis":
                return data.ChangedRoleId != 0 && condition.GetIds("roleIds").Contains(data.ChangedRoleId);
            default:
                return false;
        }
    }

    /// <summary>
    ///     Checks that a pattern is a regular expression the bot is willing to run.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    public static bool IsValidPattern(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern.Length > AutomationLimits.MaxPatternLength)
            return false;

        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, RegexTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static StringComparison Comparison(AutomationNode condition) =>
        condition.GetBool("caseSensitive") ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static bool MatchesRegex(string? pattern, string input)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        if (Patterns.Count > MaxCachedPatterns)
            Patterns.Clear();

        var regex = Patterns.GetOrAdd(pattern, p =>
        {
            try
            {
                return new Regex(p, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);
            }
            catch (ArgumentException)
            {
                return null;
            }
        });

        try
        {
            return regex?.IsMatch(input) ?? false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
