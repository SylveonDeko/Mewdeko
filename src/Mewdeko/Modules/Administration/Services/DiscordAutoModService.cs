using System.Text.RegularExpressions;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Manages Discord's native AutoMod rules and the actions Mewdeko takes when one fires.
/// </summary>
public partial class DiscordAutoModService : INService
{
    /// <summary>
    ///     The longest a rule or follow-up timeout can last, in seconds.
    /// </summary>
    public const int MaxTimeoutSeconds = 2419200;

    private const int MaxEscalateWindowMinutes = 43200;
    private const int MaxHitAgeDays = 30;

    private static readonly Regex Snowflake = SnowflakeRegex();

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<(ulong Rule, ulong User), DateTime> handled = new();
    private readonly ConcurrentDictionary<ulong, Dictionary<ulong, AutoModFollowUp>> followUps = new();
    private readonly ILogger<DiscordAutoModService> logger;
    private readonly ModCaseService modCases;
    private readonly MuteService muteService;
    private readonly GeneratedBotStrings strings;
    private readonly UserPunishService userPunishService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiscordAutoModService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="userPunishService">The warning service.</param>
    /// <param name="muteService">The mute service.</param>
    /// <param name="strings">The generated bot strings.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="modCases">The service recording moderation cases.</param>
    public DiscordAutoModService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        UserPunishService userPunishService, MuteService muteService, GeneratedBotStrings strings,
        ILogger<DiscordAutoModService> logger, ModCaseService modCases)
    {
        this.modCases = modCases;
        this.client = client;
        this.dbFactory = dbFactory;
        this.userPunishService = userPunishService;
        this.muteService = muteService;
        this.strings = strings;
        this.logger = logger;
        client.AutoModActionExecuted += OnActionExecuted;
        client.AutoModRuleDeleted += OnRuleDeleted;
    }

    [GeneratedRegex(@"\d{15,21}")]
    private static partial Regex SnowflakeRegex();

    /// <summary>
    ///     Checks that the bot can manage AutoMod rules in a guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    public static async Task<bool> CanManageAsync(IGuild guild) =>
        (await guild.GetCurrentUserAsync()).GuildPermissions.ManageGuild;

    /// <summary>
    ///     Lists a guild's native AutoMod rules.
    /// </summary>
    /// <param name="guild">The guild.</param>
    public async Task<(AutoModResult? Failure, IReadOnlyCollection<IAutoModRule> Rules)> ListAsync(IGuild guild)
    {
        if (!await CanManageAsync(guild))
            return (Fail(guild, AutoModError.MissingPermission), []);

        try
        {
            return (null, await guild.GetAutoModRulesAsync());
        }
        catch (HttpException ex)
        {
            return (Fail(guild, AutoModError.DiscordRejected, ex.Reason ?? ex.Message), []);
        }
    }

    /// <summary>
    ///     Finds a rule by ID or name.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="idOrName">The rule ID or name.</param>
    public async Task<IAutoModRule?> FindRuleAsync(IGuild guild, string idOrName)
    {
        idOrName = idOrName.Trim();
        try
        {
            var rules = await guild.GetAutoModRulesAsync();
            if (ulong.TryParse(idOrName, out var id) && rules.FirstOrDefault(x => x.Id == id) is { } byId)
                return byId;
            return rules.FirstOrDefault(x => x.Name.Equals(idOrName, StringComparison.OrdinalIgnoreCase));
        }
        catch (HttpException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Creates a native AutoMod rule.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="spec">The rule to create.</param>
    public async Task<AutoModResult> CreateAsync(IGuild guild, AutoModRuleSpec spec)
    {
        if (!await CanManageAsync(guild))
            return Fail(guild, AutoModError.MissingPermission);

        var name = spec.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100)
            return Fail(guild, AutoModError.InvalidName);

        var kind = spec.Kind ?? AutoModRuleKind.Keyword;
        var failure = await ValidateAsync(guild, kind, spec, true);
        if (failure is not null)
            return failure;

        try
        {
            var rule = await guild.CreateAutoModRuleAsync(x =>
            {
                x.Name = name;
                x.EventType = AutoModEventType.MessageSend;
                x.TriggerType = kind switch
                {
                    AutoModRuleKind.Spam => AutoModTriggerType.Spam,
                    AutoModRuleKind.Preset => AutoModTriggerType.KeywordPreset,
                    AutoModRuleKind.MentionSpam => AutoModTriggerType.MentionSpam,
                    _ => AutoModTriggerType.Keyword
                };
                x.Enabled = spec.Enabled ?? true;
                Apply(x, spec, null);
                if (spec.Actions is null)
                    x.Actions = new[]
                    {
                        new AutoModRuleActionProperties
                        {
                            Type = AutoModActionType.BlockMessage
                        }
                    };
            });
            return new AutoModResult(AutoModError.None, strings.AutomodCreated(guild.Id, rule.Name, rule.Id), rule);
        }
        catch (HttpException ex)
        {
            return Fail(guild, AutoModError.DiscordRejected, ex.Reason ?? ex.Message);
        }
    }

    /// <summary>
    ///     Changes a native AutoMod rule.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="ruleId">The rule ID.</param>
    /// <param name="spec">The changes. Null fields stay as they are.</param>
    public async Task<AutoModResult> UpdateAsync(IGuild guild, ulong ruleId, AutoModRuleSpec spec)
    {
        if (!await CanManageAsync(guild))
            return Fail(guild, AutoModError.MissingPermission);

        IAutoModRule? rule;
        try
        {
            rule = await guild.GetAutoModRuleAsync(ruleId);
        }
        catch (HttpException)
        {
            rule = null;
        }

        if (rule is null)
            return Fail(guild, AutoModError.NotFound);

        if (spec.Name is not null && (string.IsNullOrWhiteSpace(spec.Name) || spec.Name.Trim().Length > 100))
            return Fail(guild, AutoModError.InvalidName);

        var kind = KindOf(rule.TriggerType);
        var failure = await ValidateAsync(guild, kind, spec, false);
        if (failure is not null)
            return failure;

        try
        {
            await rule.ModifyAsync(x =>
            {
                if (spec.Name is not null)
                    x.Name = spec.Name.Trim();
                if (spec.Enabled is { } enabled)
                    x.Enabled = enabled;
                Apply(x, spec, rule);
            });
            var updated = await guild.GetAutoModRuleAsync(ruleId) ?? rule;
            return new AutoModResult(AutoModError.None, strings.AutomodUpdated(guild.Id, updated.Name), updated);
        }
        catch (HttpException ex)
        {
            return Fail(guild, AutoModError.DiscordRejected, ex.Reason ?? ex.Message);
        }
    }

    /// <summary>
    ///     Changes one thing about a rule from a text option and value.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="rule">The rule.</param>
    /// <param name="option">The option name.</param>
    /// <param name="value">The value, if the option takes one.</param>
    public async Task<AutoModResult> EditAsync(IGuild guild, IAutoModRule rule, string option, string? value)
    {
        value = value?.Trim();
        var off = value is null or "" || value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                  value.Equals("none", StringComparison.OrdinalIgnoreCase);
        var spec = new AutoModRuleSpec();

        switch (option.ToLowerInvariant())
        {
            case "name":
                spec.Name = value;
                break;
            case "enable":
            case "on":
                spec.Enabled = true;
                break;
            case "disable":
            case "off":
                spec.Enabled = false;
                break;
            case "keywords":
                spec.Keywords = off ? [] : SplitList(value!, ',');
                break;
            case "regex":
                spec.RegexPatterns = off ? [] : SplitList(value!, ';');
                break;
            case "allow":
                spec.AllowList = off ? [] : SplitList(value!, ',');
                break;
            case "presets":
            {
                var presets = off ? [] : ParsePresets(value!);
                if (presets.Count == 0)
                    return Fail(guild, AutoModError.MissingPresets);
                spec.Presets = presets;
                break;
            }
            case "mentionlimit":
                if (!int.TryParse(value, out var limit))
                    return Fail(guild, AutoModError.InvalidMentionLimit);
                spec.MentionLimit = limit;
                break;
            case "raidprotection":
                spec.MentionRaidProtection = !off && !value!.Equals("false", StringComparison.OrdinalIgnoreCase);
                break;
            case "block":
            {
                var actions = ActionsOf(rule).Where(x => x.Kind != AutoModActionKind.BlockMessage).ToList();
                if (!off || value is null or "")
                    actions.Add(new AutoModActionSpec
                    {
                        Kind = AutoModActionKind.BlockMessage,
                        CustomMessage = off || value!.Equals("on", StringComparison.OrdinalIgnoreCase) ? null : value
                    });
                spec.Actions = actions;
                break;
            }
            case "alert":
            {
                var actions = ActionsOf(rule).Where(x => x.Kind != AutoModActionKind.SendAlert).ToList();
                if (!off)
                {
                    var channel = ParseChannel(guild, value!);
                    if (channel is null)
                        return Fail(guild, AutoModError.InvalidValue);
                    actions.Add(new AutoModActionSpec
                    {
                        Kind = AutoModActionKind.SendAlert, ChannelId = channel.Id
                    });
                }

                spec.Actions = actions;
                break;
            }
            case "timeout":
            {
                var actions = ActionsOf(rule).Where(x => x.Kind != AutoModActionKind.Timeout).ToList();
                if (!off)
                {
                    if (!TryParseSeconds(value!, out var seconds))
                        return Fail(guild, AutoModError.InvalidValue);
                    actions.Add(new AutoModActionSpec
                    {
                        Kind = AutoModActionKind.Timeout, TimeoutSeconds = seconds
                    });
                }

                spec.Actions = actions;
                break;
            }
            case "exemptroles":
                spec.ExemptRoleIds = off ? [] : ParseRoles(guild, value!).Select(x => x.Id).ToList();
                if (!off && spec.ExemptRoleIds.Count == 0)
                    return Fail(guild, AutoModError.InvalidValue);
                break;
            case "exemptchannels":
                spec.ExemptChannelIds = off ? [] : ParseChannels(guild, value!).Select(x => x.Id).ToList();
                if (!off && spec.ExemptChannelIds.Count == 0)
                    return Fail(guild, AutoModError.InvalidValue);
                break;
            default:
                return Fail(guild, AutoModError.InvalidOption);
        }

        return await UpdateAsync(guild, rule.Id, spec);
    }

    /// <summary>
    ///     Deletes a native AutoMod rule and its follow-ups.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="ruleId">The rule ID.</param>
    public async Task<AutoModResult> DeleteAsync(IGuild guild, ulong ruleId)
    {
        if (!await CanManageAsync(guild))
            return Fail(guild, AutoModError.MissingPermission);

        try
        {
            var rule = await guild.GetAutoModRuleAsync(ruleId);
            if (rule is null)
                return Fail(guild, AutoModError.NotFound);

            await rule.DeleteAsync();
            await ClearFollowUpAsync(guild.Id, ruleId);
            return new AutoModResult(AutoModError.None, strings.AutomodDeleted(guild.Id, rule.Name));
        }
        catch (HttpException ex)
        {
            return Fail(guild, AutoModError.DiscordRejected, ex.Reason ?? ex.Message);
        }
    }

    /// <summary>
    ///     Reads the actions of a rule as specs.
    /// </summary>
    /// <param name="rule">The rule.</param>
    public static List<AutoModActionSpec> ActionsOf(IAutoModRule rule) => rule.Actions
        .Select(x => new AutoModActionSpec
        {
            Kind = x.Type switch
            {
                AutoModActionType.SendAlertMessage => AutoModActionKind.SendAlert,
                AutoModActionType.Timeout => AutoModActionKind.Timeout,
                _ => AutoModActionKind.BlockMessage
            },
            ChannelId = x.ChannelId,
            TimeoutSeconds = x.TimeoutDuration is { } span ? (int)span.TotalSeconds : null,
            CustomMessage = x.CustomMessage.IsSpecified ? x.CustomMessage.Value : null
        })
        .ToList();

    /// <summary>
    ///     Writes a rule as a short list entry.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    public string FormatRule(ulong guildId, IAutoModRule rule)
    {
        var kind = KindOf(rule.TriggerType) switch
        {
            AutoModRuleKind.Spam => strings.AutomodKindSpam(guildId),
            AutoModRuleKind.Preset => strings.AutomodKindPreset(guildId),
            AutoModRuleKind.MentionSpam => strings.AutomodKindMention(guildId),
            _ => strings.AutomodKindKeyword(guildId)
        };
        var actions = ActionsOf(rule).Select(x => x.Kind switch
        {
            AutoModActionKind.SendAlert => strings.AutomodActionAlert(guildId, x.ChannelId ?? 0),
            AutoModActionKind.Timeout => strings.AutomodActionTimeout(guildId,
                JoinRoleTimeoutService.FormatMinutes((x.TimeoutSeconds ?? 0) / 60)),
            _ => string.IsNullOrWhiteSpace(x.CustomMessage)
                ? strings.AutomodActionBlock(guildId)
                : strings.AutomodActionBlockCustom(guildId, x.CustomMessage)
        }).ToList();
        return strings.AutomodRuleEntry(guildId, rule.Name, rule.Id, kind,
            rule.Enabled ? strings.AutomodOn(guildId) : strings.AutomodOff(guildId),
            actions.Count == 0 ? "-" : string.Join(", ", actions));
    }

    /// <summary>
    ///     Writes a follow-up as the body of an embed.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="followUp">The follow-up.</param>
    public string FormatFollowUp(ulong guildId, AutoModFollowUp followUp)
    {
        var off = strings.AutomodOff(guildId);
        var punish = followUp.PunishAction == AutoModPunishAction.None
            ? off
            : strings.AutomodFollowupPunishText(guildId, followUp.PunishAction.ToString().ToLowerInvariant(),
                JoinRoleTimeoutService.FormatMinutes(followUp.PunishMinutes));
        var escalate = followUp.EscalateAction == AutoModEscalateAction.None
            ? off
            : strings.AutomodFollowupEscalateText(guildId, followUp.EscalateAction.ToString().ToLowerInvariant(),
                followUp.EscalateHits, JoinRoleTimeoutService.FormatMinutes(followUp.EscalateWindowMinutes));
        return strings.AutomodFollowupBody(guildId,
            followUp.Warn ? strings.AutomodOn(guildId) : off, punish, escalate,
            followUp.LogChannelId is { } log ? $"<#{log}>" : off);
    }

    /// <summary>
    ///     Maps a Discord trigger type to a rule kind.
    /// </summary>
    /// <param name="type">The trigger type.</param>
    public static AutoModRuleKind KindOf(AutoModTriggerType type) => type switch
    {
        AutoModTriggerType.Spam => AutoModRuleKind.Spam,
        AutoModTriggerType.KeywordPreset => AutoModRuleKind.Preset,
        AutoModTriggerType.MentionSpam => AutoModRuleKind.MentionSpam,
        _ => AutoModRuleKind.Keyword
    };

    /// <summary>
    ///     Reads presets from a list such as "profanity, sexual, slurs".
    /// </summary>
    /// <param name="input">The text.</param>
    public static List<KeywordPresetTypes> ParsePresets(string input)
    {
        var presets = new List<KeywordPresetTypes>();
        foreach (var token in input.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            KeywordPresetTypes? preset = token.ToLowerInvariant() switch
            {
                "profanity" => KeywordPresetTypes.Profanity,
                "sexual" or "sexualcontent" or "sex" => KeywordPresetTypes.SexualContent,
                "slurs" or "slur" => KeywordPresetTypes.Slurs,
                _ => null
            };
            if (preset is { } found && !presets.Contains(found))
                presets.Add(found);
        }

        return presets;
    }

    /// <summary>
    ///     Reads a duration such as 10m or 1h as seconds, up to 28 days.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="seconds">The duration in seconds.</param>
    public static bool TryParseSeconds(string input, out int seconds)
    {
        seconds = 0;
        try
        {
            seconds = (int)Math.Ceiling(StoopidTime.FromInput(input.Replace(" ", "").ToLowerInvariant()).Time
                .TotalSeconds);
            return seconds is > 0 and <= MaxTimeoutSeconds;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Reads a list of roles by mention, ID or name.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="input">The text.</param>
    public static List<IRole> ParseRoles(IGuild guild, string input)
    {
        var roles = new List<IRole>();
        foreach (var token in SplitTokens(input))
        {
            var match = Snowflake.Match(token);
            var role = match.Success && ulong.TryParse(match.Value, out var id)
                ? guild.GetRole(id)
                : guild.Roles.FirstOrDefault(x => x.Name.Equals(token, StringComparison.OrdinalIgnoreCase));
            if (role is not null && role.Id != guild.Id && roles.All(x => x.Id != role.Id))
                roles.Add(role);
        }

        return roles;
    }

    /// <summary>
    ///     Reads a list of text channels by mention, ID or name.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="input">The text.</param>
    public static List<ITextChannel> ParseChannels(IGuild guild, string input)
    {
        var channels = new List<ITextChannel>();
        foreach (var token in SplitTokens(input))
        {
            if (ParseChannel(guild, token) is { } channel && channels.All(x => x.Id != channel.Id))
                channels.Add(channel);
        }

        return channels;
    }

    /// <summary>
    ///     Reads a text channel by mention, ID or name.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="input">The text.</param>
    public static ITextChannel? ParseChannel(IGuild guild, string input)
    {
        if (guild is not SocketGuild socket)
            return null;

        var match = Snowflake.Match(input);
        if (match.Success && ulong.TryParse(match.Value, out var id))
            return socket.GetTextChannel(id);

        return socket.TextChannels.FirstOrDefault(x =>
            x.Name.Equals(input.Trim().TrimStart('#'), StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> SplitList(string value, char separator) =>
        value.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

    private static string[] SplitTokens(string input) => input.Contains(',')
        ? input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private AutoModResult Fail(IGuild guild, AutoModError error, string? detail = null) => new(error, error switch
    {
        AutoModError.MissingPermission => strings.AutomodNeedPermission(guild.Id),
        AutoModError.NotFound => strings.AutomodNotFound(guild.Id),
        AutoModError.InvalidName => strings.AutomodInvalidName(guild.Id),
        AutoModError.MissingTrigger => strings.AutomodMissingTrigger(guild.Id),
        AutoModError.InvalidMentionLimit => strings.AutomodInvalidMentionLimit(guild.Id),
        AutoModError.MissingPresets => strings.AutomodMissingPresets(guild.Id),
        AutoModError.InvalidAction => strings.AutomodInvalidAction(guild.Id),
        AutoModError.InvalidOption => strings.AutomodInvalidOption(guild.Id,
            "name, enable, disable, keywords, regex, allow, presets, mentionlimit, raidprotection, block, alert, timeout, exemptroles, exemptchannels"),
        AutoModError.InvalidValue => strings.AutomodInvalidValue(guild.Id),
        _ => strings.AutomodDiscordRejected(guild.Id, detail ?? "")
    });

    private async Task<AutoModResult?> ValidateAsync(IGuild guild, AutoModRuleKind kind, AutoModRuleSpec spec,
        bool creating)
    {
        switch (kind)
        {
            case AutoModRuleKind.Keyword when creating &&
                                              (spec.Keywords?.Count ?? 0) + (spec.RegexPatterns?.Count ?? 0) == 0:
                return Fail(guild, AutoModError.MissingTrigger);
            case AutoModRuleKind.Preset when creating && (spec.Presets?.Count ?? 0) == 0:
                return Fail(guild, AutoModError.MissingPresets);
            case AutoModRuleKind.MentionSpam when creating && spec.MentionLimit is null:
                return Fail(guild, AutoModError.InvalidMentionLimit);
        }

        if (spec.MentionLimit is < 1 or > 50)
            return Fail(guild, AutoModError.InvalidMentionLimit);

        if (spec.Actions is null)
            return null;

        var me = await guild.GetCurrentUserAsync();
        foreach (var action in spec.Actions)
        {
            switch (action.Kind)
            {
                case AutoModActionKind.SendAlert when action.ChannelId is null:
                    return Fail(guild, AutoModError.InvalidAction);
                case AutoModActionKind.Timeout when kind is not (AutoModRuleKind.Keyword or AutoModRuleKind.MentionSpam)
                                                    || action.TimeoutSeconds is null or < 1 or > MaxTimeoutSeconds:
                    return Fail(guild, AutoModError.InvalidAction);
                case AutoModActionKind.Timeout when !me.GuildPermissions.ModerateMembers:
                    return new AutoModResult(AutoModError.MissingPermission,
                        strings.AutomodNeedModerate(guild.Id));
            }
        }

        return null;
    }

    private static void Apply(AutoModRuleProperties x, AutoModRuleSpec spec, IAutoModRule? current)
    {
        if (spec.Keywords is not null)
            x.KeywordFilter = spec.Keywords.ToArray();
        if (spec.RegexPatterns is not null)
            x.RegexPatterns = spec.RegexPatterns.ToArray();
        if (spec.AllowList is not null)
            x.AllowList = spec.AllowList.ToArray();
        if (spec.Presets is not null)
            x.Presets = spec.Presets.ToArray();
        if (spec.MentionLimit is { } limit)
            x.MentionLimit = limit;
        if (spec.MentionRaidProtection is { } raid)
            x.MentionRaidProtectionEnabled = raid;
        if (spec.ExemptRoleIds is not null)
            x.ExemptRoles = spec.ExemptRoleIds.ToArray();
        if (spec.ExemptChannelIds is not null)
            x.ExemptChannels = spec.ExemptChannelIds.ToArray();
        if (spec.Actions is not null)
            x.Actions = spec.Actions.Select(a => new AutoModRuleActionProperties
            {
                Type = a.Kind switch
                {
                    AutoModActionKind.SendAlert => AutoModActionType.SendAlertMessage,
                    AutoModActionKind.Timeout => AutoModActionType.Timeout,
                    _ => AutoModActionType.BlockMessage
                },
                ChannelId = a.Kind == AutoModActionKind.SendAlert ? a.ChannelId : null,
                TimeoutDuration = a.Kind == AutoModActionKind.Timeout && a.TimeoutSeconds is { } seconds
                    ? TimeSpan.FromSeconds(seconds)
                    : null,
                CustomMessage = a.Kind == AutoModActionKind.BlockMessage && !string.IsNullOrWhiteSpace(a.CustomMessage)
                    ? a.CustomMessage
                    : null
            }).ToArray();
    }

    /// <summary>
    ///     Gets the follow-up for a rule, or null when it has none.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ruleId">The rule ID.</param>
    public async Task<AutoModFollowUp?> GetFollowUpAsync(ulong guildId, ulong ruleId) =>
        (await GetFollowUpsAsync(guildId)).GetValueOrDefault(ruleId);

    /// <summary>
    ///     Gets every follow-up in a guild by rule ID.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<Dictionary<ulong, AutoModFollowUp>> GetFollowUpsAsync(ulong guildId)
    {
        if (followUps.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var rows = await db.AutoModFollowUps.Where(x => x.GuildId == guildId).ToListAsync();
        var map = rows.ToDictionary(x => x.RuleId);
        followUps[guildId] = map;
        return map;
    }

    /// <summary>
    ///     Checks a follow-up.
    /// </summary>
    /// <param name="followUp">The follow-up.</param>
    public static bool IsValid(AutoModFollowUp followUp)
    {
        if (followUp.PunishAction != AutoModPunishAction.None && followUp.PunishMinutes < 1)
            return false;
        if (followUp.PunishAction == AutoModPunishAction.Timeout && followUp.PunishMinutes > MaxTimeoutSeconds / 60)
            return false;
        if (followUp.EscalateAction == AutoModEscalateAction.None)
            return true;
        return followUp.EscalateHits >= 2 && followUp.EscalateWindowMinutes is >= 1 and <= MaxEscalateWindowMinutes;
    }

    /// <summary>
    ///     Changes a rule's follow-up, creating it when there is none.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ruleId">The rule ID.</param>
    /// <param name="change">Applies the change to a copy.</param>
    /// <returns>Whether the result was valid and saved, and the follow-up as it is now.</returns>
    public async Task<(bool Saved, AutoModFollowUp FollowUp)> UpdateFollowUpAsync(ulong guildId, ulong ruleId,
        Action<AutoModFollowUp> change)
    {
        var current = await GetFollowUpAsync(guildId, ruleId);
        var copy = new AutoModFollowUp
        {
            Id = current?.Id ?? 0,
            GuildId = guildId,
            RuleId = ruleId,
            Warn = current?.Warn ?? false,
            WarnReason = current?.WarnReason,
            PunishAction = current?.PunishAction ?? AutoModPunishAction.None,
            PunishMinutes = current?.PunishMinutes ?? 0,
            EscalateAction = current?.EscalateAction ?? AutoModEscalateAction.None,
            EscalateHits = current?.EscalateHits ?? 0,
            EscalateWindowMinutes = current?.EscalateWindowMinutes ?? 0,
            LogChannelId = current?.LogChannelId,
            DateAdded = current?.DateAdded ?? DateTime.UtcNow
        };
        change(copy);

        if (!IsValid(copy))
            return (false, current ?? copy);

        await using var db = await dbFactory.CreateConnectionAsync();
        if (current is null)
            copy.Id = await db.InsertWithInt32IdentityAsync(copy);
        else
            await db.UpdateAsync(copy);

        (await GetFollowUpsAsync(guildId))[ruleId] = copy;
        return (true, copy);
    }

    /// <summary>
    ///     Applies one text option to a rule's follow-up.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="ruleId">The rule ID.</param>
    /// <param name="option">warn, punish, escalate, logchannel or clear.</param>
    /// <param name="value">The value for the option.</param>
    /// <returns>The reply text and whether it succeeded.</returns>
    public async Task<(bool Ok, string Message)> SetFollowUpOptionAsync(IGuild guild, ulong ruleId, string option,
        string? value)
    {
        value = value?.Trim();
        var off = value is null or "" || value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                  value.Equals("none", StringComparison.OrdinalIgnoreCase);
        var guildId = guild.Id;

        switch (option.ToLowerInvariant())
        {
            case "clear":
                await ClearFollowUpAsync(guildId, ruleId);
                return (true, strings.AutomodFollowupCleared(guildId));
            case "warn":
            {
                var on = !off;
                var reason = on && !value!.Equals("on", StringComparison.OrdinalIgnoreCase) ? value : null;
                await UpdateFollowUpAsync(guildId, ruleId, x =>
                {
                    x.Warn = on;
                    x.WarnReason = reason;
                });
                return (true, on ? strings.AutomodFollowupWarnOn(guildId) : strings.AutomodFollowupWarnOff(guildId));
            }
            case "punish":
            {
                if (off)
                {
                    await UpdateFollowUpAsync(guildId, ruleId, x =>
                    {
                        x.PunishAction = AutoModPunishAction.None;
                        x.PunishMinutes = 0;
                    });
                    return (true, strings.AutomodFollowupPunishOff(guildId));
                }

                var parts = value!.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var action = parts[0].ToLowerInvariant() switch
                {
                    "timeout" => AutoModPunishAction.Timeout,
                    "mute" => AutoModPunishAction.Mute,
                    _ => AutoModPunishAction.None
                };
                if (action == AutoModPunishAction.None || parts.Length < 2 || !TryParseSeconds(parts[1], out var seconds))
                    return (false, strings.AutomodInvalidValue(guildId));

                var minutes = (int)Math.Ceiling(seconds / 60.0);
                var (saved, _) = await UpdateFollowUpAsync(guildId, ruleId, x =>
                {
                    x.PunishAction = action;
                    x.PunishMinutes = minutes;
                });
                return saved
                    ? (true, strings.AutomodFollowupPunishSet(guildId, parts[0].ToLowerInvariant(),
                        JoinRoleTimeoutService.FormatMinutes(minutes)))
                    : (false, strings.AutomodInvalidValue(guildId));
            }
            case "escalate":
            {
                if (off)
                {
                    await UpdateFollowUpAsync(guildId, ruleId, x =>
                    {
                        x.EscalateAction = AutoModEscalateAction.None;
                        x.EscalateHits = 0;
                        x.EscalateWindowMinutes = 0;
                    });
                    return (true, strings.AutomodFollowupEscalateOff(guildId));
                }

                var parts = value!.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var action = parts[0].ToLowerInvariant() switch
                {
                    "kick" => AutoModEscalateAction.Kick,
                    "ban" => AutoModEscalateAction.Ban,
                    _ => AutoModEscalateAction.None
                };
                if (action == AutoModEscalateAction.None || parts.Length < 3 || !int.TryParse(parts[1], out var hits) ||
                    !TryParseSeconds(parts[2], out var windowSeconds))
                    return (false, strings.AutomodInvalidValue(guildId));

                var window = (int)Math.Ceiling(windowSeconds / 60.0);
                var (saved, _) = await UpdateFollowUpAsync(guildId, ruleId, x =>
                {
                    x.EscalateAction = action;
                    x.EscalateHits = hits;
                    x.EscalateWindowMinutes = window;
                });
                return saved
                    ? (true, strings.AutomodFollowupEscalateSet(guildId, parts[0].ToLowerInvariant(), hits,
                        JoinRoleTimeoutService.FormatMinutes(window)))
                    : (false, strings.AutomodInvalidValue(guildId));
            }
            case "logchannel":
            {
                if (off)
                {
                    await UpdateFollowUpAsync(guildId, ruleId, x => x.LogChannelId = null);
                    return (true, strings.AutomodFollowupLogOff(guildId));
                }

                var channel = ParseChannel(guild, value!);
                if (channel is null)
                    return (false, strings.AutomodInvalidValue(guildId));

                await UpdateFollowUpAsync(guildId, ruleId, x => x.LogChannelId = channel.Id);
                return (true, strings.AutomodFollowupLogSet(guildId, channel.Mention));
            }
            default:
                return (false, strings.AutomodInvalidOption(guildId, "warn, punish, escalate, logchannel, clear"));
        }
    }

    /// <summary>
    ///     Removes a rule's follow-up and its recorded hits.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ruleId">The rule ID.</param>
    public async Task ClearFollowUpAsync(ulong guildId, ulong ruleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.AutoModFollowUps.Where(x => x.GuildId == guildId && x.RuleId == ruleId).DeleteAsync();
        await db.AutoModHits.Where(x => x.GuildId == guildId && x.RuleId == ruleId).DeleteAsync();
        (await GetFollowUpsAsync(guildId)).Remove(ruleId);
    }

    private async Task OnRuleDeleted(SocketAutoModRule rule)
    {
        try
        {
            if ((await GetFollowUpsAsync(rule.Guild.Id)).ContainsKey(rule.Id))
                await ClearFollowUpAsync(rule.Guild.Id, rule.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not clear the AutoMod follow-up for rule {RuleId}", rule.Id);
        }
    }

    private async Task OnActionExecuted(SocketGuild guild, AutoModRuleAction action, AutoModActionExecutedData data)
    {
        try
        {
            var ruleId = data.Rule.Id;
            var followUp = await GetFollowUpAsync(guild.Id, ruleId);
            if (followUp is null)
                return;

            var userId = data.User.Id;
            var now = DateTime.UtcNow;
            if (handled.TryGetValue((ruleId, userId), out var last) && now - last < TimeSpan.FromSeconds(3))
                return;

            handled[(ruleId, userId)] = now;
            if (handled.Count > 1000)
                foreach (var key in handled.Where(x => now - x.Value > TimeSpan.FromMinutes(1)).Select(x => x.Key))
                    handled.TryRemove(key, out _);

            await RunFollowUpAsync(guild, followUp, userId, data);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AutoMod follow-up failed in {GuildId}", guild.Id);
        }
    }

    private async Task RunFollowUpAsync(SocketGuild guild, AutoModFollowUp followUp, ulong userId,
        AutoModActionExecutedData data)
    {
        var member = guild.GetUser(userId) ?? await client.Rest.GetGuildUserAsync(guild.Id, userId) as IGuildUser;
        if (member is null || member.IsBot || member.Id == guild.OwnerId)
            return;

        var me = guild.CurrentUser;
        var top = member.RoleIds.Select(guild.GetRole).Where(x => x is not null).Select(x => x.Position)
            .DefaultIfEmpty(0).Max();
        var canAct = top < me.Hierarchy;
        var rule = guild.GetAutoModRule(data.Rule.Id);
        var ruleName = rule?.Name ?? data.Rule.Id.ToString();
        var reason = strings.AutomodFollowupReason(guild.Id, ruleName);
        var done = new List<string>();

        if (followUp.Warn)
        {
            try
            {
                await userPunishService.Warn(guild, member.Id, me,
                    string.IsNullOrWhiteSpace(followUp.WarnReason) ? reason : followUp.WarnReason)!;
                done.Add(strings.AutomodLogWarned(guild.Id));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AutoMod warn failed for {UserId} in {GuildId}", member.Id, guild.Id);
            }
        }

        if (followUp.PunishAction != AutoModPunishAction.None && canAct)
        {
            var span = TimeSpan.FromMinutes(followUp.PunishMinutes);
            var text = JoinRoleTimeoutService.FormatMinutes(followUp.PunishMinutes);
            try
            {
                if (followUp.PunishAction == AutoModPunishAction.Timeout)
                {
                    await modCases.PunishAsync(guild, member, me, ModCaseAction.Timeout, reason, span,
                        () => member.SetTimeOutAsync(span, new RequestOptions
                        {
                            AuditLogReason = reason
                        })).ConfigureAwait(false);
                    done.Add(strings.AutomodLogTimedOut(guild.Id, text));
                }
                else
                {
                    await muteService.TimedMute(member, me, span, MuteType.All, reason);
                    done.Add(strings.AutomodLogMuted(guild.Id, text));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AutoMod punish failed for {UserId} in {GuildId}", member.Id, guild.Id);
            }
        }

        if (followUp.EscalateAction != AutoModEscalateAction.None && canAct &&
            await CountHitAsync(guild.Id, followUp, member.Id) >= followUp.EscalateHits)
        {
            try
            {
                var options = new RequestOptions
                {
                    AuditLogReason = reason
                };
                if (followUp.EscalateAction == AutoModEscalateAction.Kick)
                {
                    await modCases.PunishAsync(guild, member, me, ModCaseAction.Kick, reason, null,
                        () => member.KickAsync(reason, options)).ConfigureAwait(false);
                    done.Add(strings.AutomodLogKicked(guild.Id, followUp.EscalateHits));
                }
                else
                {
                    await modCases.PunishAsync(guild, member, me, ModCaseAction.Ban, reason, null,
                        () => guild.AddBanAsync(member.Id, 0, reason, options)).ConfigureAwait(false);
                    done.Add(strings.AutomodLogBanned(guild.Id, followUp.EscalateHits));
                }

                await using var db = await dbFactory.CreateConnectionAsync();
                await db.AutoModHits.Where(x => x.GuildId == guild.Id && x.RuleId == followUp.RuleId &&
                                                x.UserId == member.Id).DeleteAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AutoMod escalation failed for {UserId} in {GuildId}", member.Id, guild.Id);
            }
        }

        if (!canAct && followUp.PunishAction != AutoModPunishAction.None)
            done.Add(strings.AutomodLogHierarchy(guild.Id));

        await LogAsync(guild, followUp, member, ruleName, data, done);
    }

    private async Task<int> CountHitAsync(ulong guildId, AutoModFollowUp followUp, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var now = DateTime.UtcNow;
        await db.InsertAsync(new AutoModHit
        {
            GuildId = guildId, RuleId = followUp.RuleId, UserId = userId, HitAt = now
        });

        var cutoff = now.AddDays(-MaxHitAgeDays);
        await db.AutoModHits.Where(x => x.GuildId == guildId && x.UserId == userId && x.HitAt < cutoff).DeleteAsync();

        var since = now.AddMinutes(-followUp.EscalateWindowMinutes);
        return await db.AutoModHits.CountAsync(x => x.GuildId == guildId && x.RuleId == followUp.RuleId &&
                                                    x.UserId == userId && x.HitAt >= since);
    }

    private async Task LogAsync(SocketGuild guild, AutoModFollowUp followUp, IGuildUser member, string ruleName,
        AutoModActionExecutedData data, List<string> done)
    {
        if (followUp.LogChannelId is not { } channelId || guild.GetTextChannel(channelId) is not { } channel)
            return;

        var content = data.Content;
        if (content is { Length: > 500 })
            content = content[..500];

        var embed = new EmbedBuilder()
            .WithOkColor(guild.Id)
            .WithTitle(strings.AutomodLogTitle(guild.Id, ruleName))
            .WithDescription(strings.AutomodLogBody(guild.Id, member.Mention, member.Id,
                done.Count == 0 ? strings.AutomodLogNothing(guild.Id) : string.Join(", ", done)))
            .WithCurrentTimestamp();
        if (!string.IsNullOrWhiteSpace(content))
            embed.AddField(strings.AutomodLogContent(guild.Id), content);

        try
        {
            await channel.SendMessageAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None);
        }
        catch (HttpException ex)
        {
            logger.LogDebug(ex, "Could not write to the AutoMod log channel in {GuildId}", guild.Id);
        }
    }
}
