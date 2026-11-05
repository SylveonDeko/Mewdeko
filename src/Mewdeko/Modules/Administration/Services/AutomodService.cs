using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using Humanizer;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Runs the caps filter, attachment spam, emoji spam, spoiler spam, zalgo text, media-only, image-only, video-only,
///     commands-only channel and unsafe file rules, each with a list of actions.
/// </summary>
public class AutomodService : INService, IReadyExecutor
{
    /// <summary>
    ///     The file extensions blocked until a server changes the list.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultExtensions =
    [
        "exe", "scr", "bat", "cmd", "msi", "msp", "js", "jse", "vbs", "vbe", "wsf", "wsh", "ps1", "jar", "apk", "com",
        "dll", "lnk", "hta", "reg", "cpl", "pif", "scf", "inf", "application", "gadget"
    ];

    private static readonly Regex Ignored = new(@"https?://\S+|<a?:\w+:\d+>|<[@#][&!]?\d+>|<t:\d+(:\w)?>",
        RegexOptions.Compiled);

    private static readonly Regex Link = new(@"https?://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly AutomodRule[] CheckOrder =
    [
        AutomodRule.Files, AutomodRule.Commands, AutomodRule.Media, AutomodRule.ImageOnly, AutomodRule.VideoOnly,
        AutomodRule.Attachments, AutomodRule.Spoilers, AutomodRule.Emoji, AutomodRule.Zalgo, AutomodRule.Caps
    ];

    private static readonly TimeSpan EmbedWait = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<(ulong, ulong), Queue<(DateTime At, int Count)>> attachmentHistory = new();
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<SocketMessage>> embedWaiters = new();
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly AutomodExtrasService extras;
    private readonly ILogger<AutomodService> logger;
    private readonly UserPunishService punishService;
    private readonly ConcurrentDictionary<(ulong, AutomodRule), Compiled> rules = new();
    private readonly GeneratedBotStrings strings;
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutomodService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="punishService">The user punish service.</param>
    /// <param name="strings">The generated bot strings.</param>
    /// <param name="extras">The automod extras service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="logger">The logger instance.</param>
    public AutomodService(DiscordShardedClient client, IDataConnectionFactory dbFactory, EventHandler eventHandler,
        UserPunishService punishService, GeneratedBotStrings strings, AutomodExtrasService extras,
        GuildSettingsService guildSettings, ILogger<AutomodService> logger)
    {
        this.guildSettings = guildSettings;
        this.client = client;
        this.dbFactory = dbFactory;
        this.extras = extras;
        this.punishService = punishService;
        this.strings = strings;
        this.logger = logger;
        eventHandler.Subscribe("MessageReceived", "AutomodService", OnMessage);
        eventHandler.Subscribe("MessageUpdated", "AutomodService", OnMessageUpdated);
    }

    /// <summary>
    ///     Loads every guild's rules into the cache.
    /// </summary>
    public async Task OnReadyAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var all = await db.GetTable<AutomodRuleSetting>().ToListAsync().ConfigureAwait(false);
            foreach (var setting in all)
                rules[(setting.GuildId, setting.Rule)] = new Compiled(setting);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load automod rules");
        }

        _ = Task.Run(PruneAttachmentHistoryAsync);
    }

    private async Task PruneAttachmentHistoryAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync())
        {
            var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(10);
            foreach (var (key, history) in attachmentHistory)
            {
                lock (history)
                {
                    while (history.TryPeek(out var oldest) && oldest.At < cutoff)
                        history.Dequeue();
                    if (history.Count == 0)
                        attachmentHistory.TryRemove(key, out _);
                }
            }
        }
    }

    /// <summary>
    ///     Gets a rule's settings from the cache.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>The settings, or null when the rule was never configured.</returns>
    public AutomodRuleSetting? Get(ulong guildId, AutomodRule rule)
    {
        return rules.TryGetValue((guildId, rule), out var compiled) ? compiled.Setting : null;
    }

    /// <summary>
    ///     Gets the extensions a guild's unsafe files rule blocks.
    /// </summary>
    /// <param name="setting">The rule's settings, or null for the default list.</param>
    public static IReadOnlyList<string> GetExtensions(AutomodRuleSetting? setting)
    {
        return setting?.Extensions is null
            ? DefaultExtensions
            : setting.Extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    ///     Reads a list of action names such as "delete warn dm".
    /// </summary>
    /// <param name="input">The text to read.</param>
    /// <param name="invalid">The first word that is not an action.</param>
    /// <returns>The actions, or none when nothing valid was given.</returns>
    public static AutomodAction ParseActions(string? input, out string? invalid)
    {
        invalid = null;
        var result = AutomodAction.None;
        foreach (var word in (input ?? "").Split([' ', ',', '+', '/'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (word.ToLowerInvariant())
            {
                case "delete" or "del" or "remove":
                    result |= AutomodAction.Delete;
                    break;
                case "warn":
                    result |= AutomodAction.Warn;
                    break;
                case "timeout" or "to":
                    result |= AutomodAction.Timeout;
                    break;
                case "mute":
                    result |= AutomodAction.Mute;
                    break;
                case "kick":
                    result |= AutomodAction.Kick;
                    break;
                case "ban":
                    result |= AutomodAction.Ban;
                    break;
                case "message" or "msg" or "say":
                    result |= AutomodAction.Message;
                    break;
                case "dm":
                    result |= AutomodAction.Dm;
                    break;
                case "review":
                    result |= AutomodAction.Review;
                    break;
                default:
                    invalid ??= word;
                    break;
            }
        }

        return result;
    }

    /// <summary>
    ///     Writes actions as a short readable list.
    /// </summary>
    /// <param name="actions">The actions.</param>
    public static string DescribeActions(AutomodAction actions)
    {
        var names = Enum.GetValues<AutomodAction>()
            .Where(x => x != AutomodAction.None && actions.HasFlag(x))
            .Select(x => x.ToString().ToLowerInvariant())
            .ToList();
        return names.Count == 0 ? "none" : string.Join(", ", names);
    }

    /// <summary>
    ///     Creates or changes a rule's settings and refreshes the cache.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    /// <param name="change">What to change.</param>
    /// <returns>The saved settings.</returns>
    public async Task<AutomodRuleSetting> UpdateAsync(ulong guildId, AutomodRule rule,
        Action<AutomodRuleSetting> change)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.GetTable<AutomodRuleSetting>()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Rule == rule).ConfigureAwait(false);
        var isNew = row is null;
        row ??= new AutomodRuleSetting
        {
            GuildId = guildId, Rule = rule
        };

        change(row);

        if (isNew)
            row.Id = await db.InsertWithInt32IdentityAsync(row).ConfigureAwait(false);
        else
            await db.UpdateAsync(row).ConfigureAwait(false);

        rules[(guildId, rule)] = new Compiled(row);
        return row;
    }

    /// <summary>
    ///     Turns a rule off, keeping its settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>True when the rule was on.</returns>
    public async Task<bool> DisableAsync(ulong guildId, AutomodRule rule)
    {
        if (Get(guildId, rule) is not { Enabled: true })
            return false;

        await UpdateAsync(guildId, rule, x => x.Enabled = false).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     Toggles a role or channel as exempt from a rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    /// <param name="id">The role or channel ID.</param>
    /// <param name="isRole">Whether the ID is a role.</param>
    /// <returns>True when it was added, false when it was removed.</returns>
    public async Task<bool> ToggleExemptAsync(ulong guildId, AutomodRule rule, ulong id, bool isRole)
    {
        var added = false;
        await UpdateAsync(guildId, rule, x =>
        {
            var ids = JoinRoleTimeoutService.ParseIds(isRole ? x.ExemptRoleIds : x.ExemptChannelIds);
            added = !ids.Remove(id);
            if (added) ids.Add(id);
            var joined = JoinRoleTimeoutService.JoinIds(ids);
            if (isRole) x.ExemptRoleIds = joined;
            else x.ExemptChannelIds = joined;
        }).ConfigureAwait(false);
        return added;
    }

    /// <summary>
    ///     Toggles a channel in the list of a channel rule: media-only, image-only, video-only or commands-only.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The channel rule.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True when it was added, false when it was removed.</returns>
    public async Task<bool> ToggleChannelAsync(ulong guildId, AutomodRule rule, ulong channelId)
    {
        var added = false;
        await UpdateAsync(guildId, rule, x =>
        {
            var ids = JoinRoleTimeoutService.ParseIds(x.MediaChannelIds);
            added = !ids.Remove(channelId);
            if (added) ids.Add(channelId);
            x.MediaChannelIds = JoinRoleTimeoutService.JoinIds(ids);
        }).ConfigureAwait(false);
        return added;
    }

    /// <summary>
    ///     Adds an extension to the blocked list, or removes it when it is already there.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="extension">The extension, with or without the dot.</param>
    /// <returns>True when it was added, false when it was removed.</returns>
    public async Task<bool> ToggleExtensionAsync(ulong guildId, string extension)
    {
        var normalized = extension.Trim().TrimStart('.').ToLowerInvariant();
        var added = false;
        await UpdateAsync(guildId, AutomodRule.Files, x =>
        {
            var list = GetExtensions(x).ToList();
            added = !list.Remove(normalized);
            if (added) list.Add(normalized);
            x.Extensions = string.Join(' ', list);
        }).ConfigureAwait(false);
        return added;
    }

    /// <summary>
    ///     Puts the blocked extensions back to the default list.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public Task ResetExtensionsAsync(ulong guildId)
    {
        return UpdateAsync(guildId, AutomodRule.Files, x => x.Extensions = null);
    }

    /// <summary>
    ///     Gets the short name of a rule for messages.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    public string RuleName(ulong guildId, AutomodRule rule)
    {
        return rule switch
        {
            AutomodRule.Caps => strings.AutomodRuleCaps(guildId),
            AutomodRule.Attachments => strings.AutomodRuleAttachments(guildId),
            AutomodRule.Media => strings.AutomodRuleMedia(guildId),
            AutomodRule.Emoji => strings.AutomodRuleEmoji(guildId),
            AutomodRule.Spoilers => strings.AutomodRuleSpoilers(guildId),
            AutomodRule.Zalgo => strings.AutomodRuleZalgo(guildId),
            AutomodRule.Commands => strings.AutomodRuleCommands(guildId),
            AutomodRule.ImageOnly => strings.AutomodRuleImageOnly(guildId),
            AutomodRule.VideoOnly => strings.AutomodRuleVideoOnly(guildId),
            _ => strings.AutomodRuleFiles(guildId)
        };
    }

    /// <summary>
    ///     Builds the settings text for a rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>The text, or null when the rule is not on.</returns>
    public string? Describe(ulong guildId, AutomodRule rule)
    {
        if (Get(guildId, rule) is not { Enabled: true } s)
            return null;

        var actions = DescribeActions(s.Actions);
        var duration = s.Actions.HasFlag(AutomodAction.Timeout) || s.Actions.HasFlag(AutomodAction.Mute)
            ? $" ({TimeSpan.FromMinutes(s.ActionMinutes).Humanize()})"
            : "";
        var roles = FormatIds(s.ExemptRoleIds, "<@&{0}>");
        var channels = FormatIds(s.ExemptChannelIds, "<#{0}>");

        return rule switch
        {
            AutomodRule.Caps => strings.AutomodCapsStats(guildId, s.MinLength, s.CapsPercent, actions + duration,
                roles, channels),
            AutomodRule.Attachments => strings.AutomodAttachmentsStats(guildId, s.MaxAttachments, s.WindowSeconds,
                actions + duration, roles, channels),
            AutomodRule.Media or AutomodRule.ImageOnly or AutomodRule.VideoOnly or AutomodRule.Commands =>
                strings.AutomodMediaStats(guildId, FormatIds(s.MediaChannelIds, "<#{0}>"),
                    s.AllowThreads ? strings.AutomodYes(guildId) : strings.AutomodNo(guildId), actions + duration,
                    roles, channels),
            AutomodRule.Emoji => strings.AutomodEmojiStats(guildId, s.MaxEmojis, actions + duration, roles, channels),
            AutomodRule.Spoilers => strings.AutomodSpoilersStats(guildId, s.MaxSpoilers, actions + duration, roles,
                channels),
            AutomodRule.Zalgo => strings.AutomodZalgoStats(guildId, s.ZalgoThreshold, actions + duration, roles,
                channels),
            _ => strings.AutomodFilesStats(guildId, string.Join(", ", GetExtensions(s)), actions + duration, roles,
                channels)
        };
    }

    /// <summary>
    ///     Turns a rule on with the actions a member typed.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    /// <param name="actionText">The actions, for example "delete warn".</param>
    /// <param name="change">Rule specific settings to apply.</param>
    /// <returns>Whether it worked, and the message to show.</returns>
    public async Task<(bool Ok, string Message)> EnableRuleAsync(ulong guildId, AutomodRule rule, string? actionText,
        Action<AutomodRuleSetting> change)
    {
        var error = ValidateActions(guildId, actionText, out var actions);
        if (error is not null)
            return (false, error);

        await UpdateAsync(guildId, rule, x =>
        {
            x.Enabled = true;
            x.Actions = actions;
            change(x);
        }).ConfigureAwait(false);

        return (true, strings.AutomodEnabled(guildId, RuleName(guildId, rule), DescribeActions(actions)));
    }

    /// <summary>
    ///     Turns a rule off.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>Whether it was on, and the message to show.</returns>
    public async Task<(bool Ok, string Message)> DisableRuleAsync(ulong guildId, AutomodRule rule)
    {
        var name = RuleName(guildId, rule);
        return await DisableAsync(guildId, rule).ConfigureAwait(false)
            ? (true, strings.AutomodDisabled(guildId, name))
            : (false, strings.AutomodNotEnabled(guildId, name));
    }

    /// <summary>
    ///     Gets the display name of an existing protection.
    /// </summary>
    /// <param name="target">The protection.</param>
    public static string ProtectionName(ProtectionOptionTarget target)
    {
        return target switch
        {
            ProtectionOptionTarget.Spam => "Anti-Spam",
            ProtectionOptionTarget.MassMention => "Anti-Mass-Mention",
            ProtectionOptionTarget.Pattern => "Anti-Pattern",
            _ => "Anti-Mass-Post"
        };
    }

    /// <summary>
    ///     Checks a list of actions typed by a member.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="input">The text to read.</param>
    /// <param name="actions">The actions that were read.</param>
    /// <returns>An error message, or null when the actions are fine.</returns>
    public string? ValidateActions(ulong guildId, string? input, out AutomodAction actions)
    {
        actions = ParseActions(input, out var invalid);
        if (invalid is not null)
            return strings.AutomodUnknownAction(guildId, invalid);

        if (actions == AutomodAction.None)
            return strings.AutomodNoActions(guildId);

        return actions.HasFlag(AutomodAction.Review) && extras.Get(guildId).ReviewChannelId is null
            ? strings.AutomodReviewNeedsChannel(guildId)
            : null;
    }

    /// <summary>
    ///     Builds the embed showing every automod rule and the extra options on the existing protections.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="protection">The protection service.</param>
    public EmbedBuilder BuildOverview(ulong guildId, ProtectionService protection)
    {
        var embed = new EmbedBuilder().WithOkColor().WithTitle(strings.AutomodOverviewTitle(guildId));
        foreach (var rule in Enum.GetValues<AutomodRule>())
        {
            embed.AddField(RuleName(guildId, rule),
                (Describe(guildId, rule) ?? strings.AutomodOverviewOff(guildId)).TrimTo(1024), true);
        }

        foreach (var info in protection.GetProtectionOptions(guildId))
        {
            embed.AddField(ProtectionName(info.Target), strings.AutomodProtectionOptions(guildId,
                info.DeleteMessage ? strings.AutomodYes(guildId) : strings.AutomodNo(guildId),
                info.NotifyUser ? strings.AutomodYes(guildId) : strings.AutomodNo(guildId),
                FormatIds(string.Join(' ', info.ExemptRoles), "<@&{0}>"),
                FormatIds(string.Join(' ', info.ExemptChannels), "<#{0}>")).TrimTo(1024), true);
        }

        var shared = extras.Get(guildId);
        embed.AddField(strings.AutomodExtrasTitle(guildId), strings.AutomodExtrasStats(guildId,
            shared.LogChannelId is { } log ? $"<#{log}>" : "-",
            shared.ReviewChannelId is { } review ? $"<#{review}>" : "-",
            FormatIds(shared.ExemptRoleIds, "<@&{0}>"), FormatIds(shared.ExemptChannelIds, "<#{0}>"),
            shared.NormalizeWords ? strings.AutomodYes(guildId) : strings.AutomodNo(guildId)).TrimTo(1024));

        return embed;
    }

    private string FormatIds(string stored, string format)
    {
        var ids = JoinRoleTimeoutService.ParseIds(stored);
        return ids.Count == 0 ? "-" : string.Join(' ', ids.Select(x => string.Format(format, x)));
    }

    private Task OnMessage(IMessage arg)
    {
        if (arg is not SocketUserMessage msg || msg.Author.IsBot || msg.Author is not SocketGuildUser member ||
            msg.Channel is not SocketGuildChannel channel || msg.Type is not (MessageType.Default or MessageType.Reply))
            return Task.CompletedTask;

        var guildId = channel.Guild.Id;
        var active = CheckOrder.Select(x => rules.GetValueOrDefault((guildId, x)))
            .Where(x => x is { Setting.Enabled: true })
            .ToList();
        if (active.Count == 0 || member.GuildPermissions.Administrator)
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var compiled in active)
                {
                    if (compiled is null || extras.IsExempt(member, channel) || IsExempt(compiled, member, channel))
                        continue;

                    var prefix = compiled.Setting.Rule == AutomodRule.Commands
                        ? await guildSettings.GetPrefix(channel.Guild).ConfigureAwait(false)
                        : null;
                    var judged = msg;
                    if (compiled.Setting.Rule is AutomodRule.ImageOnly or AutomodRule.VideoOnly &&
                        InChannelList(compiled, channel) && !HasMedia(compiled.Setting.Rule, msg) &&
                        Link.IsMatch(msg.Content))
                        judged = await WaitForEmbedsAsync(msg).ConfigureAwait(false);

                    if (!Triggers(compiled, judged, channel, prefix))
                        continue;

                    await ApplyAsync(compiled.Setting, member, msg).ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing automod for user {UserId}", member.Id);
            }
        });

        return Task.CompletedTask;
    }

    private static bool IsExempt(Compiled compiled, SocketGuildUser member, SocketGuildChannel channel)
    {
        if (member.Roles.Any(r => compiled.ExemptRoles.Contains(r.Id)))
            return true;

        if (compiled.ExemptChannels.Contains(channel.Id))
            return true;

        return channel is SocketThreadChannel { ParentChannel: { } parent } && compiled.ExemptChannels.Contains(parent.Id);
    }

    private bool Triggers(Compiled compiled, SocketUserMessage msg, SocketGuildChannel channel, string? prefix)
    {
        var s = compiled.Setting;
        switch (s.Rule)
        {
            case AutomodRule.Files:
                return msg.Attachments.Any(a => compiled.Extensions.Contains(Path.GetExtension(a.Filename)
                    .TrimStart('.').ToLowerInvariant()));

            case AutomodRule.Media or AutomodRule.ImageOnly or AutomodRule.VideoOnly or AutomodRule.Commands:
            {
                var parentId = (channel as SocketThreadChannel)?.ParentChannel?.Id;
                var inThread = parentId is not null;
                var listed = compiled.MediaChannels.Contains(channel.Id) ||
                             (inThread && compiled.MediaChannels.Contains(parentId!.Value));
                if (!listed || (inThread && s.AllowThreads))
                    return false;

                return s.Rule switch
                {
                    AutomodRule.Commands => !IsCommand(msg.Content, prefix),
                    AutomodRule.ImageOnly or AutomodRule.VideoOnly => !HasMedia(s.Rule, msg),
                    _ => msg.Attachments.Count == 0 && msg.Stickers.Count == 0 &&
                         !msg.Embeds.Any(e => e.Image is not null || e.Thumbnail is not null || e.Video is not null) &&
                         !Link.IsMatch(msg.Content)
                };
            }

            case AutomodRule.Emoji:
                return AutomodDetectors.CountEmojis(msg.Content) > s.MaxEmojis;

            case AutomodRule.Spoilers:
                return AutomodDetectors.CountSpoilers(msg.Content, msg.Attachments.Select(a => a.Filename)) >
                       s.MaxSpoilers;

            case AutomodRule.Zalgo:
                return AutomodDetectors.MaxCombiningMarks(msg.Content) > s.ZalgoThreshold;

            case AutomodRule.Attachments:
            {
                if (msg.Attachments.Count == 0)
                    return false;

                var now = DateTime.UtcNow;
                var window = TimeSpan.FromSeconds(Math.Max(1, s.WindowSeconds));
                var history = attachmentHistory.GetOrAdd((channel.Guild.Id, msg.Author.Id), _ => new());
                lock (history)
                {
                    while (history.TryPeek(out var oldest) && now - oldest.At > window)
                        history.Dequeue();

                    history.Enqueue((now, msg.Attachments.Count));
                    if (history.Sum(x => x.Count) <= s.MaxAttachments)
                        return false;

                    history.Clear();
                    return true;
                }
            }

            default:
            {
                var text = Ignored.Replace(msg.Content, "");
                if (text.Length < s.MinLength)
                    return false;

                var letters = text.Count(char.IsLetter);
                if (letters == 0)
                    return false;

                return text.Count(char.IsUpper) * 100 / letters >= s.CapsPercent;
            }
        }
    }

    private static bool HasMedia(AutomodRule rule, SocketMessage msg)
    {
        return HasAttachedMedia(rule, msg) || (rule == AutomodRule.ImageOnly
            ? AutomodDetectors.HasImageLink(msg.Content)
            : AutomodDetectors.HasVideoLink(msg.Content));
    }

    private static bool HasAttachedMedia(AutomodRule rule, SocketMessage msg)
    {
        return rule == AutomodRule.ImageOnly
            ? msg.Attachments.Any(a => a.ContentType?.StartsWith("image/") == true ||
                                       AutomodDetectors.IsImageName(a.Filename)) ||
              msg.Embeds.Any(e => e.Type is EmbedType.Image or EmbedType.Gifv ||
                                  (e.Type == EmbedType.Rich && e.Image is not null))
            : msg.Attachments.Any(a => a.ContentType?.StartsWith("video/") == true ||
                                       AutomodDetectors.IsVideoName(a.Filename)) ||
              msg.Embeds.Any(e => e.Type == EmbedType.Video || e.Video is not null);
    }

    private bool InChannelList(Compiled compiled, SocketGuildChannel channel)
    {
        var parentId = (channel as SocketThreadChannel)?.ParentChannel?.Id;
        var inThread = parentId is not null;
        var listed = compiled.MediaChannels.Contains(channel.Id) ||
                     (inThread && compiled.MediaChannels.Contains(parentId!.Value));
        return listed && !(inThread && compiled.Setting.AllowThreads);
    }

    private async Task<SocketUserMessage> WaitForEmbedsAsync(SocketUserMessage msg)
    {
        var waiter = new TaskCompletionSource<SocketMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        embedWaiters[msg.Id] = waiter;
        try
        {
            if (msg.Embeds.Count > 0)
                return msg;

            await Task.WhenAny(waiter.Task, Task.Delay(EmbedWait)).ConfigureAwait(false);
            return waiter.Task.IsCompletedSuccessfully && waiter.Task.Result is SocketUserMessage updated
                ? updated
                : msg;
        }
        finally
        {
            embedWaiters.TryRemove(msg.Id, out _);
        }
    }

    private Task OnMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
    {
        if (after.Embeds.Count > 0 && embedWaiters.TryGetValue(after.Id, out var waiter))
            waiter.TrySetResult(after);

        return Task.CompletedTask;
    }

    private bool IsCommand(string content, string? prefix)
    {
        var text = content.TrimStart();
        if (!string.IsNullOrEmpty(prefix) && text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;

        var id = client.CurrentUser.Id;
        return text.StartsWith($"<@{id}>") || text.StartsWith($"<@!{id}>");
    }

    private async Task ApplyAsync(AutomodRuleSetting s, SocketGuildUser member, SocketUserMessage msg)
    {
        var guild = member.Guild;
        var reason = RuleName(guild.Id, s.Rule);

        if (s.Actions.HasFlag(AutomodAction.Delete))
        {
            try
            {
                await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not delete automod message {MessageId}", msg.Id);
            }
        }

        if (s.Actions.HasFlag(AutomodAction.Message))
        {
            try
            {
                var text = !string.IsNullOrWhiteSpace(s.ActionMessage)
                    ? s.ActionMessage.Replace("%user%", member.Mention)
                    : s.Rule == AutomodRule.Commands
                        ? strings.AutomodCommandsNotice(guild.Id, member.Mention)
                        : strings.AutomodDefaultMessage(guild.Id, member.Mention, reason);
                var sent = await msg.Channel.SendMessageAsync(text,
                    allowedMentions: new AllowedMentions(AllowedMentionTypes.Users)).ConfigureAwait(false);
                _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(async _ =>
                {
                    try
                    {
                        await sent.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Already gone
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not send automod message in {ChannelId}", msg.Channel.Id);
            }
        }

        if (s.Actions.HasFlag(AutomodAction.Dm))
        {
            try
            {
                var dm = await member.CreateDMChannelAsync().ConfigureAwait(false);
                await dm.SendMessageAsync(strings.AutomodDm(guild.Id, guild.Name, reason)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not DM {UserId} about automod", member.Id);
            }
        }

        var held = s.Actions.HasFlag(AutomodAction.Review) && await PostReviewAsync(s, member, msg)
            .ConfigureAwait(false);
        var done = held ? s.Actions & (AutomodAction.Delete | AutomodAction.Message | AutomodAction.Dm) : s.Actions;
        if (held)
            done |= AutomodAction.Review;
        else
            await ApplyPunishmentsAsync(s, member).ConfigureAwait(false);

        await extras.LogAsync(guild.Id, member.Id, reason, msg.Content, DescribeActions(done), msg.Channel.Id)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Applies the punishments of a rule to a member: warn, timeout, mute, kick and ban.
    /// </summary>
    /// <param name="s">The rule's settings.</param>
    /// <param name="member">The member.</param>
    public async Task ApplyPunishmentsAsync(AutomodRuleSetting s, SocketGuildUser member)
    {
        var guild = member.Guild;
        var reason = RuleName(guild.Id, s.Rule);
        var minutes = Math.Clamp(s.ActionMinutes, 1, 40320);
        var punishments = new List<(PunishmentAction Action, int Minutes)>();
        if (s.Actions.HasFlag(AutomodAction.Warn)) punishments.Add((PunishmentAction.Warn, 0));
        if (s.Actions.HasFlag(AutomodAction.Timeout)) punishments.Add((PunishmentAction.Timeout, minutes));
        if (s.Actions.HasFlag(AutomodAction.Mute)) punishments.Add((PunishmentAction.Mute, minutes));
        if (s.Actions.HasFlag(AutomodAction.Ban)) punishments.Add((PunishmentAction.Ban, 0));
        else if (s.Actions.HasFlag(AutomodAction.Kick)) punishments.Add((PunishmentAction.Kick, 0));

        foreach (var (action, length) in punishments)
        {
            try
            {
                await punishService.ApplyPunishment(guild, member, client.CurrentUser, action, length, null,
                    $"Automod: {reason}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Automod could not apply {Action} to {UserId} in {GuildId}", action, member.Id,
                    guild.Id);
            }
        }
    }

    private async Task<bool> PostReviewAsync(AutomodRuleSetting s, SocketGuildUser member, SocketUserMessage msg)
    {
        var guildId = member.Guild.Id;
        if (extras.Get(guildId).ReviewChannelId is not { } channelId ||
            member.Guild.GetTextChannel(channelId) is not { } channel)
            return false;

        try
        {
            var content = string.IsNullOrWhiteSpace(msg.Content)
                ? string.Join('\n', msg.Attachments.Select(x => x.Filename))
                : msg.Content;
            var embed = new EmbedBuilder().WithErrorColor()
                .WithTitle(strings.AutomodReviewTitle(guildId))
                .AddField(strings.AutomodActionMember(guildId), $"<@{member.Id}> ({member.Id})", true)
                .AddField(strings.AutomodActionRule(guildId), RuleName(guildId, s.Rule), true)
                .AddField(strings.AutomodActionChannel(guildId), $"<#{msg.Channel.Id}>", true)
                .AddField(strings.AutomodReviewOnApprove(guildId), DescribePunishments(s), true)
                .AddField(strings.AutomodActionMessage(guildId),
                    string.IsNullOrWhiteSpace(content) ? strings.AutomodActionNoText(guildId) : content.TrimTo(1000));

            var components = new ComponentBuilder()
                .WithButton(strings.AutomodReviewApprove(guildId), $"automodreview:approve:{(int)s.Rule}:{member.Id}",
                    ButtonStyle.Danger)
                .WithButton(strings.AutomodReviewDismiss(guildId), $"automodreview:dismiss:{(int)s.Rule}:{member.Id}",
                    ButtonStyle.Secondary);

            await channel.SendMessageAsync(embed: embed.Build(), components: components.Build(),
                allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not post an automod review in {ChannelId}", channelId);
            return false;
        }
    }

    private string DescribePunishments(AutomodRuleSetting s)
    {
        var punishments = s.Actions & (AutomodAction.Warn | AutomodAction.Timeout | AutomodAction.Mute |
                                       AutomodAction.Kick | AutomodAction.Ban);
        var text = DescribeActions(punishments);
        return s.Actions.HasFlag(AutomodAction.Timeout) || s.Actions.HasFlag(AutomodAction.Mute)
            ? $"{text} ({TimeSpan.FromMinutes(s.ActionMinutes).Humanize()})"
            : text;
    }

    private sealed class Compiled
    {
        public Compiled(AutomodRuleSetting setting)
        {
            Setting = setting;
            ExemptRoles = JoinRoleTimeoutService.ParseIds(setting.ExemptRoleIds).ToHashSet();
            ExemptChannels = JoinRoleTimeoutService.ParseIds(setting.ExemptChannelIds).ToHashSet();
            MediaChannels = JoinRoleTimeoutService.ParseIds(setting.MediaChannelIds).ToHashSet();
            Extensions = GetExtensions(setting).ToHashSet();
        }

        public AutomodRuleSetting Setting { get; }
        public HashSet<ulong> ExemptRoles { get; }
        public HashSet<ulong> ExemptChannels { get; }
        public HashSet<ulong> MediaChannels { get; }
        public HashSet<string> Extensions { get; }
    }
}
