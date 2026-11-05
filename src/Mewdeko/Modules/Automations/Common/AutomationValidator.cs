using System.Text.Json;
using System.Text.RegularExpressions;
using DataModel;

namespace Mewdeko.Modules.Automations.Common;

/// <summary>
///     Something wrong with an automation, and where.
/// </summary>
/// <param name="Path">Where the problem is, such as <c>actions[1].roleIds</c>.</param>
/// <param name="Message">What is wrong, in plain words.</param>
public sealed record AutomationValidationError(string Path, string Message);

/// <summary>
///     Checks that an automation is complete and safe before it is saved.
/// </summary>
public static partial class AutomationValidator
{
    /// <summary>
    ///     Checks a whole automation.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="trigger">The trigger.</param>
    /// <param name="conditions">The conditions.</param>
    /// <param name="actions">The actions.</param>
    /// <param name="cooldownSeconds">The cooldown in seconds.</param>
    /// <returns>Every problem found. Empty when the automation can be saved.</returns>
    public static List<AutomationValidationError> Validate(string? name, AutomationTriggerType trigger,
        IReadOnlyList<AutomationNode> conditions, IReadOnlyList<AutomationNode> actions, int cooldownSeconds)
    {
        var errors = new List<AutomationValidationError>();

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > AutomationLimits.MaxNameLength)
            errors.Add(new AutomationValidationError("name",
                $"The name must be 1 to {AutomationLimits.MaxNameLength} characters."));

        if (!Enum.IsDefined(trigger))
        {
            errors.Add(new AutomationValidationError("trigger", "That trigger does not exist."));
            return errors;
        }

        if (cooldownSeconds is < 0 or > AutomationLimits.MaxCooldownSeconds)
            errors.Add(new AutomationValidationError("cooldownSeconds",
                $"The cooldown must be 0 to {AutomationLimits.MaxCooldownSeconds} seconds."));

        var provides = AutomationSchema.GetTrigger(trigger).Provides;

        if (conditions.Count > AutomationLimits.MaxConditions)
            errors.Add(new AutomationValidationError("conditions",
                $"An automation can have at most {AutomationLimits.MaxConditions} conditions."));

        if (actions.Count == 0)
            errors.Add(new AutomationValidationError("actions", "Add at least one action."));
        else if (actions.Count > AutomationLimits.MaxActions)
            errors.Add(new AutomationValidationError("actions",
                $"An automation can have at most {AutomationLimits.MaxActions} actions."));

        for (var i = 0; i < Math.Min(conditions.Count, AutomationLimits.MaxConditions); i++)
            ValidateNode($"conditions[{i}]", conditions[i], AutomationSchema.FindCondition(conditions[i].Type),
                trigger, provides, errors);

        var threadAvailable = provides.HasFlag(AutomationContext.Thread);
        var totalWait = 0L;
        for (var i = 0; i < Math.Min(actions.Count, AutomationLimits.MaxActions); i++)
        {
            var action = actions[i];
            var path = $"actions[{i}]";
            var spec = AutomationSchema.FindAction(action.Type);
            ValidateNode(path, action, spec, trigger, provides, errors);
            if (spec is null)
                continue;

            switch (spec.Type)
            {
                case "SendMessage":
                    RequireContent(path, action, errors);
                    if (action.GetId("channelId") == 0 && !provides.HasFlag(AutomationContext.Channel))
                        errors.Add(new AutomationValidationError($"{path}.channelId",
                            "This trigger has no channel, so pick one."));
                    if (action.GetBool("ephemeral") &&
                        (trigger != AutomationTriggerType.ButtonClicked || action.GetId("channelId") != 0))
                        errors.Add(new AutomationValidationError($"{path}.ephemeral",
                            "Only a reply to a button click can be ephemeral, so leave the channel empty."));
                    break;
                case "SendDm":
                case "ReplyToMessage":
                    RequireContent(path, action, errors);
                    break;
                case "SendInThread":
                    RequireContent(path, action, errors);
                    if (!threadAvailable)
                        errors.Add(new AutomationValidationError(path, "Create a thread before this action."));
                    break;
                case "AddThreadMembers":
                    if (!threadAvailable)
                        errors.Add(new AutomationValidationError(path, "Create a thread before this action."));
                    if (action.GetIds("userIds").Count == 0 && !action.GetBool("addTriggerUser"))
                        errors.Add(new AutomationValidationError($"{path}.userIds", "Pick who to add."));
                    break;
                case "CreateThread":
                    threadAvailable = true;
                    if (action.GetId("channelId") == 0 && !provides.HasFlag(AutomationContext.Channel))
                        errors.Add(new AutomationValidationError($"{path}.channelId",
                            "This trigger has no channel, so pick one."));
                    break;
                case "Wait":
                    totalWait += action.GetLong("seconds");
                    break;
            }

            if (action.GetString("target")?.Equals("MessageAuthor", StringComparison.OrdinalIgnoreCase) == true &&
                !provides.HasFlag(AutomationContext.Message))
                errors.Add(new AutomationValidationError($"{path}.target", "This trigger has no message."));
        }

        if (totalWait > AutomationLimits.MaxTotalWaitSeconds)
            errors.Add(new AutomationValidationError("actions",
                $"Waits can add up to at most {AutomationLimits.MaxTotalWaitSeconds} seconds."));

        return errors;
    }

    /// <summary>
    ///     Checks the roles and channels an automation points at against what exists in the guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="conditions">The conditions.</param>
    /// <param name="actions">The actions.</param>
    public static List<AutomationValidationError> ValidateAgainstGuild(SocketGuild guild,
        IReadOnlyList<AutomationNode> conditions, IReadOnlyList<AutomationNode> actions)
    {
        var errors = new List<AutomationValidationError>();
        Check("conditions", conditions);
        Check("actions", actions);
        return errors;

        void Check(string list, IReadOnlyList<AutomationNode> nodes)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var spec = list == "conditions"
                    ? AutomationSchema.FindCondition(nodes[i].Type)
                    : AutomationSchema.FindAction(nodes[i].Type);
                if (spec is null)
                    continue;

                foreach (var field in spec.Fields)
                {
                    var path = $"{list}[{i}].{field.Name}";
                    switch (field.Kind)
                    {
                        case AutomationFieldKind.Channel:
                            if (nodes[i].GetId(field.Name) is var channel and not 0 &&
                                guild.GetChannel(channel) is null)
                                errors.Add(new AutomationValidationError(path, "That channel is not in this server."));
                            break;
                        case AutomationFieldKind.ChannelList:
                            foreach (var id in nodes[i].GetIds(field.Name))
                            {
                                if (guild.GetChannel(id) is null)
                                    errors.Add(new AutomationValidationError(path,
                                        $"{id} is not a channel in this server."));
                            }

                            break;
                        case AutomationFieldKind.Role:
                        case AutomationFieldKind.RoleList:
                            var roles = field.Kind == AutomationFieldKind.Role
                                ? new List<ulong> { nodes[i].GetId(field.Name) }
                                : nodes[i].GetIds(field.Name);
                            foreach (var id in roles.Where(x => x != 0))
                            {
                                var role = guild.GetRole(id);
                                if (role is null)
                                    errors.Add(new AutomationValidationError(path,
                                        $"{id} is not a role in this server."));
                                else if (list == "actions" && (role.IsManaged || role.IsEveryone))
                                    errors.Add(new AutomationValidationError(path,
                                        $"{role.Name} cannot be given or taken."));
                            }

                            break;
                    }
                }
            }
        }
    }

    private static void RequireContent(string path, AutomationNode node, List<AutomationValidationError> errors)
    {
        if (node.GetString("text") is null && node.GetString("embed") is null)
            errors.Add(new AutomationValidationError(path, "Add text or an embed."));
    }

    private static void ValidateNode(string path, AutomationNode node, AutomationNodeSpec? spec,
        AutomationTriggerType trigger, AutomationContext provides, List<AutomationValidationError> errors)
    {
        if (spec is null)
        {
            errors.Add(new AutomationValidationError($"{path}.type", $"{node.Type} is not a known type."));
            return;
        }

        if ((spec.Requires & ~provides) != 0)
            errors.Add(new AutomationValidationError($"{path}.type", $"{spec.Name} does not work with {trigger}."));

        var known = spec.Fields.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in node.Fields.Keys.Where(x => !known.Contains(x)))
            errors.Add(new AutomationValidationError($"{path}.{key}", $"{spec.Name} has no {key} field."));

        foreach (var field in spec.Fields)
            ValidateField($"{path}.{field.Name}", field, node, errors);
    }

    private static void ValidateField(string path, AutomationFieldSpec field, AutomationNode node,
        List<AutomationValidationError> errors)
    {
        if (!node.TryGet(field.Name, out var value))
        {
            if (field.Required)
                errors.Add(new AutomationValidationError(path, "This field is required."));
            return;
        }

        void Fail(string message) => errors.Add(new AutomationValidationError(path, message));

        switch (field.Kind)
        {
            case AutomationFieldKind.Text:
            case AutomationFieldKind.LongText:
            {
                var maxLength = field.Max ?? AutomationLimits.MaxTextLength;
                if (value.ValueKind != JsonValueKind.String)
                    Fail("This has to be text.");
                else if (field.Required && string.IsNullOrWhiteSpace(value.GetString()))
                    Fail("This field is required.");
                else if (value.GetString()!.Length > maxLength)
                    Fail($"This can be at most {maxLength} characters.");
                else if (field.Name == "pattern" && !AutomationConditionEvaluator.IsValidPattern(value.GetString()))
                    Fail("That is not a valid regular expression.");
                else if (field.Name == "customId" && !CustomIdPattern().IsMatch(value.GetString()!))
                    Fail("Use letters, numbers, dashes, underscores, dots and colons.");
                break;
            }
            case AutomationFieldKind.EmbedJson:
                if (value.ValueKind != JsonValueKind.String)
                    Fail("This has to be text.");
                else if (value.GetString()!.Length > AutomationLimits.MaxEmbedLength)
                    Fail($"This can be at most {AutomationLimits.MaxEmbedLength} characters.");
                else if (!string.IsNullOrWhiteSpace(value.GetString()) &&
                         !SmartEmbed.TryParse(value.GetString()!, null, out _, out _, out _))
                    Fail("That is not valid embed JSON.");
                break;
            case AutomationFieldKind.Integer:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number))
                    Fail("This has to be a whole number.");
                else if (number < (field.Min ?? long.MinValue) || number > (field.Max ?? long.MaxValue))
                    Fail($"This has to be between {field.Min} and {field.Max}.");
                break;
            case AutomationFieldKind.Boolean:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    Fail("This has to be true or false.");
                break;
            case AutomationFieldKind.Channel:
            case AutomationFieldKind.Role:
            case AutomationFieldKind.User:
                if (!IsId(value))
                    Fail("This has to be an ID.");
                break;
            case AutomationFieldKind.Emoji:
                if (value.ValueKind != JsonValueKind.String || !AutomationEmoji.IsValid(value.GetString()))
                    Fail("That is not an emoji.");
                break;
            case AutomationFieldKind.Choice:
                if (value.ValueKind != JsonValueKind.String ||
                    !field.Options!.Contains(value.GetString(), StringComparer.OrdinalIgnoreCase))
                    Fail($"Use one of {string.Join(", ", field.Options!)}.");
                break;
            case AutomationFieldKind.ChannelList:
            case AutomationFieldKind.RoleList:
            case AutomationFieldKind.UserList:
            case AutomationFieldKind.EmojiList:
            case AutomationFieldKind.ChoiceList:
                ValidateList(field, value, Fail);
                break;
            case AutomationFieldKind.Buttons:
                ValidateButtons(node, field, value, Fail);
                break;
        }
    }

    private static void ValidateList(AutomationFieldSpec field, JsonElement value, Action<string> fail)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            fail("This has to be a list.");
            return;
        }

        var items = value.EnumerateArray().ToList();
        var min = field.Required ? Math.Max(1, field.Min ?? 1) : 0;
        if (items.Count < min)
            fail("Add at least one.");
        else if (items.Count > AutomationLimits.MaxListItems)
            fail($"At most {AutomationLimits.MaxListItems} are allowed.");

        foreach (var item in items.Take(AutomationLimits.MaxListItems))
        {
            var valid = field.Kind switch
            {
                AutomationFieldKind.EmojiList => item.ValueKind == JsonValueKind.String &&
                                                 AutomationEmoji.IsValid(item.GetString()),
                AutomationFieldKind.ChoiceList => item.ValueKind == JsonValueKind.String &&
                                                  field.Options!.Contains(item.GetString(),
                                                      StringComparer.OrdinalIgnoreCase),
                _ => IsId(item)
            };

            if (!valid)
            {
                fail(field.Kind == AutomationFieldKind.ChoiceList
                    ? $"Use any of {string.Join(", ", field.Options!)}."
                    : "One of these is not valid.");
                return;
            }
        }
    }

    private static void ValidateButtons(AutomationNode node, AutomationFieldSpec field, JsonElement value,
        Action<string> fail)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            fail("This has to be a list.");
            return;
        }

        var max = (int)(field.Max ?? AutomationLimits.MaxButtons);
        if (value.GetArrayLength() > max)
        {
            fail($"A message can have at most {max} buttons.");
            return;
        }

        var buttons = node.GetButtons(field.Name);
        if (buttons.Count != value.GetArrayLength())
        {
            fail("Each button has to be an object.");
            return;
        }

        foreach (var button in buttons)
        {
            if (!CustomIdPattern().IsMatch(button.CustomId) || button.CustomId.Length > 60)
                fail("A button needs a custom ID of letters, numbers, dashes, underscores, dots and colons.");
            else if (button.Label.Length is 0 or > 80)
                fail("A button label has to be 1 to 80 characters.");
            else if (!AutomationSchema.ButtonStyles.Contains(button.Style, StringComparer.OrdinalIgnoreCase))
                fail($"A button style has to be one of {string.Join(", ", AutomationSchema.ButtonStyles)}.");
            else if (button.Emoji is not null && !AutomationEmoji.IsValid(button.Emoji))
                fail("A button emoji is not valid.");
            else
                continue;

            return;
        }

        if (buttons.Select(x => x.CustomId).Distinct().Count() != buttons.Count)
            fail("Each button needs its own custom ID.");
    }

    private static bool IsId(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.TryGetUInt64(out var id) && id != 0,
        JsonValueKind.String => ulong.TryParse(value.GetString(), out var id) && id != 0,
        _ => false
    };

    [GeneratedRegex(@"^[A-Za-z0-9_\-.:]+$")]
    private static partial Regex CustomIdPattern();
}
