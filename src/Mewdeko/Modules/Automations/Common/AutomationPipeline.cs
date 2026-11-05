using System.Text.Json;
using System.Text.Json.Nodes;
using Mewdeko.Common.Replacements;
using Mewdeko.Modules.Chat_Triggers.Common;

namespace Mewdeko.Modules.Automations.Common;

/// <summary>
///     The parts of running an automation that do not touch Discord: ordering, the action cap, retries, waits and
///     placeholder replacement.
/// </summary>
public static class AutomationPipeline
{
    /// <summary>
    ///     Performs actions in order and stops at the first one that fails.
    /// </summary>
    /// <param name="actions">The actions. Only the first <see cref="AutomationLimits.MaxActions" /> are performed.</param>
    /// <param name="retry">Whether transient failures are tried again.</param>
    /// <param name="execute">Performs one action.</param>
    /// <param name="delay">Waits between retries.</param>
    /// <param name="onError">Told about each failure that ends the run.</param>
    public static async Task<AutomationRunResult> RunAsync(IReadOnlyList<AutomationNode> actions, bool retry,
        Func<AutomationNode, Task> execute, Func<TimeSpan, Task> delay, Action<Exception, AutomationNode>? onError = null)
    {
        var attempts = retry ? AutomationLimits.MaxAttempts : 1;

        for (var i = 0; i < Math.Min(actions.Count, AutomationLimits.MaxActions); i++)
        {
            var action = actions[i];
            for (var attempt = 1;; attempt++)
            {
                try
                {
                    await execute(action);
                    break;
                }
                catch (Exception ex) when (attempt < attempts && AutomationRetry.IsTransient(ex))
                {
                    await delay(AutomationRetry.Backoff(attempt));
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex, action);
                    return new AutomationRunResult(false, i, action.Type, AutomationRetry.Describe(ex));
                }
            }
        }

        return AutomationRunResult.Ok;
    }

    /// <summary>
    ///     Works out how long a wait action may wait, within the per-wait and per-run caps.
    /// </summary>
    /// <param name="requested">The seconds the action asks for.</param>
    /// <param name="alreadyWaited">The seconds this run has already waited.</param>
    /// <returns>The seconds to wait.</returns>
    /// <exception cref="AutomationActionException">The run has used up its waiting time.</exception>
    public static int NextWait(long requested, int alreadyWaited)
    {
        var seconds = (int)Math.Clamp(requested, 1, AutomationLimits.MaxWaitSeconds);
        if (alreadyWaited + seconds > AutomationLimits.MaxTotalWaitSeconds)
            throw new AutomationActionException("This run has waited as long as it is allowed to.");

        return seconds;
    }

    /// <summary>
    ///     Builds the text and embeds of a message action, with placeholders replaced.
    /// </summary>
    /// <param name="node">The action holding <c>text</c> and <c>embed</c> fields.</param>
    /// <param name="replacer">The placeholder replacer.</param>
    /// <param name="allowMassMentions">Whether @everyone and @here are left alone.</param>
    /// <param name="guildId">The guild ID, for parsing the embed.</param>
    /// <exception cref="AutomationActionException">The embed is invalid or the message comes out empty.</exception>
    public static (string? Text, Discord.Embed[]? Embeds) Compose(AutomationNode node, Replacer replacer,
        bool allowMassMentions, ulong guildId)
    {
        var text = replacer.Replace(node.GetString("text"));
        if (text is not null)
            text = new TriggerBlockEvaluator().Evaluate(text);

        Discord.Embed[]? embeds = null;
        if (node.GetString("embed") is { } json)
        {
            var rendered = RenderJson(json, replacer);
            if (!SmartEmbed.TryParse(rendered, guildId, out embeds, out var plain, out _))
                throw new AutomationActionException("The embed JSON is not valid.");

            if (!string.IsNullOrWhiteSpace(plain))
                text = text is null ? plain : $"{text}\n{plain}";
        }

        if (!allowMassMentions && text is not null)
            text = text.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(text) && (embeds is null || embeds.Length == 0))
            throw new AutomationActionException("The message came out empty.");

        return (string.IsNullOrWhiteSpace(text) ? null : text.TrimTo(2000), embeds);
    }

    /// <summary>
    ///     Replaces placeholders inside every string value of embed JSON, so values holding quotes or line breaks cannot
    ///     break the JSON. Text that is not JSON has the placeholders replaced as plain text.
    /// </summary>
    /// <param name="json">The embed JSON.</param>
    /// <param name="replacer">The placeholder replacer.</param>
    public static string RenderJson(string json, Replacer replacer)
    {
        try
        {
            if (JsonNode.Parse(json) is not { } root)
                return replacer.Replace(json) ?? json;

            return Walk(root, replacer)?.ToJsonString() ?? json;
        }
        catch (JsonException)
        {
            return replacer.Replace(json) ?? json;
        }
    }

    private static JsonNode? Walk(JsonNode? node, Replacer replacer)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(x => x.Key).ToList())
                    obj[key] = Walk(obj[key], replacer);
                return obj;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                    array[i] = Walk(array[i], replacer);
                return array;
            case JsonValue value when value.TryGetValue<string>(out var text):
                return JsonValue.Create(replacer.Replace(text));
            default:
                return node;
        }
    }
}
