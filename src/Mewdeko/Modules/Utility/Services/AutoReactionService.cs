using System.Globalization;
using System.Text.RegularExpressions;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Patreon.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Why an auto reaction change was refused.
/// </summary>
public enum AutoReactionError
{
    /// <summary>
    ///     Nothing is wrong.
    /// </summary>
    None,

    /// <summary>
    ///     The rule does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The rule has no emojis.
    /// </summary>
    NoEmojis,

    /// <summary>
    ///     The rule has more emojis than allowed.
    /// </summary>
    TooManyEmojis,

    /// <summary>
    ///     A word list is too long or has a word that is too long.
    /// </summary>
    InvalidWords,

    /// <summary>
    ///     The channel cannot be reacted in.
    /// </summary>
    InvalidChannel
}

/// <summary>
///     Adds reactions to new messages in channels with an auto reaction rule.
/// </summary>
public class AutoReactionService : INService
{
    /// <summary>
    ///     The most emojis a rule can have.
    /// </summary>
    public const int MaxEmojis = 10;

    /// <summary>
    ///     The most words a rule can have in each word list.
    /// </summary>
    public const int MaxWords = 25;

    /// <summary>
    ///     The longest a word can be.
    /// </summary>
    public const int MaxWordLength = 50;

    private static readonly Regex LinkRegex = new(@"https?://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".avif"];
    private static readonly string[] VideoExtensions = [".mp4", ".mov", ".webm", ".mkv", ".avi", ".m4v"];

    private readonly ConcurrentDictionary<ulong, List<AutoReaction>> cache = new();
    private readonly ConcurrentDictionary<ulong, ulong?> managerRoles = new();
    private static readonly Regex SnowflakeRegex = new(@"\d{15,21}", RegexOptions.Compiled);

    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<AutoReactionService> logger;
    private readonly ConcurrentDictionary<(int Rule, string Emoji), byte> rejected = new();
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutoReactionService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="strings">The generated bot strings.</param>
    /// <param name="logger">The logger instance.</param>
    public AutoReactionService(IDataConnectionFactory dbFactory, EventHandler eventHandler,
        GeneratedBotStrings strings, ILogger<AutoReactionService> logger)
    {
        this.strings = strings;
        this.dbFactory = dbFactory;
        this.logger = logger;
        eventHandler.Subscribe("MessageReceived", "AutoReactionService", OnMessageReceived);
        eventHandler.Subscribe("LeftGuild", "AutoReactionService", OnLeftGuild);
    }

    /// <summary>
    ///     Splits a stored space separated list of IDs.
    /// </summary>
    /// <param name="value">The stored value.</param>
    public static List<ulong> ParseIds(string? value) =>
        (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => ulong.TryParse(x, out var id) ? id : 0)
        .Where(x => x != 0)
        .Distinct()
        .ToList();

    /// <summary>
    ///     Pulls IDs out of text such as mentions or plain numbers.
    /// </summary>
    /// <param name="text">The text.</param>
    public static List<ulong> ExtractIds(string? text) =>
        SnowflakeRegex.Matches(text ?? "")
            .Select(x => ulong.TryParse(x.Value, out var id) ? id : 0)
            .Where(x => x != 0)
            .Distinct()
            .ToList();

    /// <summary>
    ///     Gets the message that explains why a change was refused.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="error">The error.</param>
    /// <param name="id">The rule ID, when there is one.</param>
    /// <param name="prefix">The guild's command prefix.</param>
    public string ErrorMessage(ulong guildId, AutoReactionError error, int id, string prefix) => error switch
    {
        AutoReactionError.NotFound => strings.AutoReactNotFound(guildId, id, prefix),
        AutoReactionError.NoEmojis => strings.AutoReactNoEmojis(guildId),
        AutoReactionError.TooManyEmojis => strings.AutoReactTooManyEmojis(guildId, MaxEmojis),
        AutoReactionError.InvalidWords => strings.AutoReactBadWords(guildId, MaxWords, MaxWordLength),
        _ => strings.AutoReactBadChannel(guildId)
    };

    /// <summary>
    ///     Builds the embed that lists a guild's rules.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rules">The rules.</param>
    public Discord.Embed ListEmbed(ulong guildId, List<AutoReaction> rules) => new EmbedBuilder()
        .WithOkColor(guildId)
        .WithTitle(strings.AutoReactListTitle(guildId))
        .WithDescription(string.Join('\n', rules.Select((x, i) => strings.AutoReactListEntry(guildId, x.Id,
            $"<#{x.ChannelId}>", string.Join(' ', x.Emojis.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
            x.MessageTypes.ToString().ToLowerInvariant(),
            x.Enabled ? strings.AutoReactOn(guildId) : strings.AutoReactOff(guildId), i + 1))))
        .Build();

    /// <summary>
    ///     Builds the embed that shows one rule.
    /// </summary>
    /// <param name="rule">The rule.</param>
    public Discord.Embed ShowEmbed(AutoReaction rule)
    {
        var guildId = rule.GuildId;
        var none = strings.AutoReactNone(guildId);
        string OnOff(bool value) => value ? strings.AutoReactOn(guildId) : strings.AutoReactOff(guildId);
        string Mentions(string stored, string prefix) =>
            ParseIds(stored).Count == 0 ? none : string.Join(' ', ParseIds(stored).Select(x => $"<{prefix}{x}>"));
        string Words(string stored) => ParseWords(stored).Count == 0 ? none : string.Join(", ", ParseWords(stored));

        return new EmbedBuilder()
            .WithOkColor(guildId)
            .WithTitle(strings.AutoReactShowTitle(guildId, rule.Id))
            .WithDescription(strings.AutoReactShowBody(guildId,
                $"<#{rule.ChannelId}>",
                rule.Emojis,
                rule.MessageTypes.ToString().ToLowerInvariant(),
                OnOff(rule.Enabled),
                OnOff(rule.IgnoreBots),
                OnOff(rule.IgnoreWebhooks),
                OnOff(rule.IgnoreReplies),
                Mentions(rule.AllowedRoleIds, "@&"),
                Mentions(rule.IgnoredRoleIds, "@&"),
                Mentions(rule.IgnoredUserIds, "@"),
                Words(rule.SkipWords),
                Words(rule.RequiredWords),
                OnOff(rule.DeleteNonMatching)))
            .Build();
    }

    /// <summary>
    ///     Joins IDs into the stored space separated form.
    /// </summary>
    /// <param name="ids">The IDs.</param>
    public static string JoinIds(IEnumerable<ulong> ids) => string.Join(' ', ids.Distinct());

    /// <summary>
    ///     Splits a stored newline separated list of words.
    /// </summary>
    /// <param name="value">The stored value.</param>
    public static List<string> ParseWords(string? value) =>
        (value ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>
    ///     Joins words into the stored newline separated form, dropping blanks and repeats.
    /// </summary>
    /// <param name="words">The words.</param>
    /// <returns>The stored value, or null when the list is too long or has a word that is too long.</returns>
    public static string? JoinWords(IEnumerable<string> words)
    {
        var list = words.Select(x => x.Trim().Replace('\n', ' '))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (list.Count > MaxWords || list.Any(x => x.Length > MaxWordLength))
            return null;
        return string.Join('\n', list);
    }

    /// <summary>
    ///     Splits stored emojis into the emojis to react with.
    /// </summary>
    /// <param name="value">The stored value.</param>
    public static List<IEmote> ParseEmojis(string? value) =>
        (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.TryToIEmote(out var emote) ? emote : null)
        .OfType<IEmote>()
        .ToList();

    /// <summary>
    ///     Reads emojis from text, either unicode or custom emojis from the guild.
    /// </summary>
    /// <param name="guild">The guild whose custom emojis are allowed.</param>
    /// <param name="input">The text with the emojis.</param>
    /// <param name="stored">The emojis in the stored form.</param>
    /// <returns>The first part of the text that is not a usable emoji, or null when all of it is.</returns>
    public static string? TryParseEmojis(IGuild guild, string input, out string stored)
    {
        var result = new List<string>();
        foreach (var token in input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Emote.TryParse(token, out var emote))
            {
                if (guild.Emotes.All(x => x.Id != emote.Id))
                {
                    stored = "";
                    return token;
                }

                result.Add(emote.ToString());
                continue;
            }

            var elements = StringInfo.GetTextElementEnumerator(token);
            while (elements.MoveNext())
            {
                var element = elements.GetTextElement();
                if (!Emoji.TryParse(element, out var emoji))
                {
                    stored = "";
                    return element;
                }

                result.Add(emoji.Name);
            }
        }

        stored = string.Join(' ', result);
        return null;
    }

    /// <summary>
    ///     Gets a guild's rules.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<List<AutoReaction>> GetRulesAsync(ulong guildId)
    {
        if (cache.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var rules = await db.AutoReactions.Where(x => x.GuildId == guildId).OrderBy(x => x.Priority)
            .ThenBy(x => x.Id).ToListAsync();
        cache[guildId] = rules;
        return rules;
    }

    /// <summary>
    ///     Gets the role whose members can configure auto reactions without Manage Server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The role ID, or null when none is set.</returns>
    public async Task<ulong?> GetManagerRoleAsync(ulong guildId)
    {
        if (managerRoles.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var roleId = await db.AutoReactionSettings.Where(x => x.GuildId == guildId)
            .Select(x => x.ManagerRoleId).FirstOrDefaultAsync();
        managerRoles[guildId] = roleId;
        return roleId;
    }

    /// <summary>
    ///     Sets or clears the role whose members can configure auto reactions without Manage Server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="roleId">The role ID, or null to clear it.</param>
    public async Task SetManagerRoleAsync(ulong guildId, ulong? roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = await db.AutoReactionSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (existing is null)
        {
            await db.InsertAsync(new AutoReactionSetting
            {
                GuildId = guildId, ManagerRoleId = roleId
            });
        }
        else
        {
            existing.ManagerRoleId = roleId;
            await db.UpdateAsync(existing);
        }

        managerRoles[guildId] = roleId;
    }

    /// <summary>
    ///     Checks whether a member can configure auto reactions, by Manage Server or by the manager role.
    /// </summary>
    /// <param name="user">The member.</param>
    public async Task<bool> CanManageAsync(IGuildUser user)
    {
        if (user.GuildPermissions.ManageGuild)
            return true;

        return await GetManagerRoleAsync(user.GuildId) is { } roleId && user.RoleIds.Contains(roleId);
    }

    /// <summary>
    ///     Moves a rule to a position in the order, which sets the order its reactions are added in.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The rule ID.</param>
    /// <param name="position">The new position, counting from 1.</param>
    /// <returns>Whether the rule existed, and the position it ended up in.</returns>
    public async Task<(bool Found, int Position)> MoveAsync(ulong guildId, int id, int position)
    {
        var rules = (await GetRulesAsync(guildId)).ToList();
        var rule = rules.FirstOrDefault(x => x.Id == id);
        if (rule is null)
            return (false, 0);

        rules.Remove(rule);
        var index = Math.Clamp(position - 1, 0, rules.Count);
        rules.Insert(index, rule);

        await SaveOrderAsync(guildId, rules);
        return (true, index + 1);
    }

    /// <summary>
    ///     Sets the order of a guild's rules. Rules that are not listed go last in their current order.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ids">The rule IDs, first to last.</param>
    public async Task ReorderAsync(ulong guildId, IEnumerable<int> ids)
    {
        var rules = await GetRulesAsync(guildId);
        var ordered = ids.Distinct().Select(id => rules.FirstOrDefault(x => x.Id == id)).OfType<AutoReaction>()
            .ToList();
        ordered.AddRange(rules.Where(x => !ordered.Contains(x)));
        await SaveOrderAsync(guildId, ordered);
    }

    private async Task SaveOrderAsync(ulong guildId, List<AutoReaction> ordered)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        for (var i = 0; i < ordered.Count; i++)
        {
            var priority = i;
            var ruleId = ordered[i].Id;
            await db.AutoReactions.Where(x => x.Id == ruleId)
                .Set(x => x.Priority, priority)
                .UpdateAsync();
        }

        await ReloadAsync(guildId);
    }

    /// <summary>
    ///     Gets one of a guild's rules.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The rule ID.</param>
    public async Task<AutoReaction?> GetRuleAsync(ulong guildId, int id) =>
        (await GetRulesAsync(guildId)).FirstOrDefault(x => x.Id == id);

    /// <summary>
    ///     Checks a rule.
    /// </summary>
    /// <param name="rule">The rule to check.</param>
    public static AutoReactionError Validate(AutoReaction rule)
    {
        var emojis = rule.Emojis.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (emojis == 0)
            return AutoReactionError.NoEmojis;
        if (emojis > MaxEmojis)
            return AutoReactionError.TooManyEmojis;
        return AutoReactionError.None;
    }

    /// <summary>
    ///     Adds a rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule to add.</param>
    public async Task<(AutoReactionError Error, AutoReaction? Rule)> AddAsync(ulong guildId, AutoReaction rule)
    {
        var error = Validate(rule);
        if (error != AutoReactionError.None)
            return (error, null);

        rule.GuildId = guildId;
        rule.DateAdded = DateTime.UtcNow;
        rule.Priority = (await GetRulesAsync(guildId)).Select(x => x.Priority + 1).DefaultIfEmpty(0).Max();

        await using var db = await dbFactory.CreateConnectionAsync();
        rule.Id = await db.InsertWithInt32IdentityAsync(rule);
        await ReloadAsync(guildId);
        return (AutoReactionError.None, rule);
    }

    /// <summary>
    ///     Changes a rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The rule ID.</param>
    /// <param name="change">Applies the change to a copy of the rule.</param>
    public async Task<(AutoReactionError Error, AutoReaction? Rule)> UpdateAsync(ulong guildId, int id,
        Action<AutoReaction> change)
    {
        var current = await GetRuleAsync(guildId, id);
        if (current is null)
            return (AutoReactionError.NotFound, null);

        var copy = Clone(current);
        change(copy);

        var error = Validate(copy);
        if (error != AutoReactionError.None)
            return (error, current);

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(copy);
        await ReloadAsync(guildId);
        return (AutoReactionError.None, copy);
    }

    /// <summary>
    ///     Removes a rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The rule ID.</param>
    /// <returns>Whether the rule existed.</returns>
    public async Task<bool> RemoveAsync(ulong guildId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var removed = await db.AutoReactions.Where(x => x.GuildId == guildId && x.Id == id).DeleteAsync() > 0;
        if (removed)
            await ReloadAsync(guildId);
        return removed;
    }

    /// <summary>
    ///     Checks whether a message is one a rule reacts to.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="message">The message.</param>
    public static bool Matches(AutoReaction rule, IUserMessage message)
    {
        if (!AppliesToAuthor(rule, message))
            return false;

        var content = message.Content ?? "";
        var skip = ParseWords(rule.SkipWords);
        if (skip.Any(x => content.Contains(x, StringComparison.OrdinalIgnoreCase)))
            return false;
        var required = ParseWords(rule.RequiredWords);
        if (required.Count > 0 && !required.Any(x => content.Contains(x, StringComparison.OrdinalIgnoreCase)))
            return false;

        return MatchesMessageType(rule, message);
    }

    /// <summary>
    ///     Checks whether a message is one a rule cleans up: its author is not exempt and the message does not fit the
    ///     rule's message type.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="message">The message.</param>
    public static bool ShouldDelete(AutoReaction rule, IUserMessage message) =>
        rule.DeleteNonMatching && rule.MessageTypes != AutoReactionMessageType.Any &&
        AppliesToAuthor(rule, message) && !MatchesMessageType(rule, message);

    private static bool AppliesToAuthor(AutoReaction rule, IUserMessage message)
    {
        if (message.Type is not (MessageType.Default or MessageType.Reply))
            return false;

        var author = message.Author;
        if (ParseIds(rule.IgnoredUserIds).Contains(author.Id))
            return false;
        if (author.IsWebhook ? rule.IgnoreWebhooks : rule.IgnoreBots && author.IsBot)
            return false;
        if (rule.IgnoreReplies && (message.Type == MessageType.Reply || message.Reference?.MessageId.IsSpecified == true))
            return false;

        var allowed = ParseIds(rule.AllowedRoleIds);
        var ignored = ParseIds(rule.IgnoredRoleIds);
        if (allowed.Count > 0 || ignored.Count > 0)
        {
            var roles = author is IGuildUser member ? member.RoleIds : [];
            if (ignored.Any(roles.Contains))
                return false;
            if (allowed.Count > 0 && !allowed.Any(roles.Contains))
                return false;
        }

        return true;
    }

    private static bool MatchesMessageType(AutoReaction rule, IUserMessage message)
    {
        var content = message.Content ?? "";
        return rule.MessageTypes switch
        {
            AutoReactionMessageType.Text => message.Attachments.Count == 0 && message.Embeds.Count == 0 &&
                                            !LinkRegex.IsMatch(content),
            AutoReactionMessageType.Images => HasImage(message),
            AutoReactionMessageType.Videos => HasVideo(message),
            AutoReactionMessageType.Media => HasImage(message) || HasVideo(message),
            AutoReactionMessageType.Links => LinkRegex.IsMatch(content),
            _ => true
        };
    }

    private static bool HasImage(IUserMessage message) =>
        message.Attachments.Any(x => IsKind(x, "image/", ImageExtensions)) ||
        message.Embeds.Any(x => x.Type is EmbedType.Image or EmbedType.Gifv);

    private static bool HasVideo(IUserMessage message) =>
        message.Attachments.Any(x => IsKind(x, "video/", VideoExtensions)) ||
        message.Embeds.Any(x => x.Type == EmbedType.Video);

    private static bool IsKind(IAttachment attachment, string contentTypePrefix, string[] extensions)
    {
        if (attachment.ContentType is { } type)
            return type.StartsWith(contentTypePrefix, StringComparison.OrdinalIgnoreCase);

        var name = attachment.Filename ?? "";
        return extensions.Any(x => name.EndsWith(x, StringComparison.OrdinalIgnoreCase));
    }

    private static AutoReaction Clone(AutoReaction source) => new()
    {
        Id = source.Id,
        GuildId = source.GuildId,
        ChannelId = source.ChannelId,
        Emojis = source.Emojis,
        MessageTypes = source.MessageTypes,
        IgnoreBots = source.IgnoreBots,
        IgnoreWebhooks = source.IgnoreWebhooks,
        IgnoreReplies = source.IgnoreReplies,
        AllowedRoleIds = source.AllowedRoleIds,
        IgnoredRoleIds = source.IgnoredRoleIds,
        IgnoredUserIds = source.IgnoredUserIds,
        SkipWords = source.SkipWords,
        RequiredWords = source.RequiredWords,
        Priority = source.Priority,
        DeleteNonMatching = source.DeleteNonMatching,
        Enabled = source.Enabled,
        DateAdded = source.DateAdded
    };

    private async Task ReloadAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        cache[guildId] = await db.AutoReactions.Where(x => x.GuildId == guildId).OrderBy(x => x.Priority)
            .ThenBy(x => x.Id).ToListAsync();
        foreach (var key in rejected.Keys)
            rejected.TryRemove(key, out _);
    }

    private Task OnLeftGuild(SocketGuild guild)
    {
        cache.TryRemove(guild.Id, out _);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceived(SocketMessage socketMessage)
    {
        if (socketMessage is not SocketUserMessage message || message.Channel is not SocketGuildChannel channel)
            return;

        try
        {
            var targetId = channel.Id;
            if (channel is SocketThreadChannel { ParentChannel: IForumChannel forum } thread)
            {
                if (message.Id != thread.Id)
                    return;
                targetId = forum.Id;
            }

            var perms = channel.Guild.CurrentUser.GetPermissions(channel);
            var channelRules = (await GetRulesAsync(channel.Guild.Id))
                .Where(x => x.Enabled && x.ChannelId == targetId)
                .ToList();

            if (targetId == channel.Id && perms.ManageMessages && channelRules.Any(x => ShouldDelete(x, message)))
            {
                await message.DeleteAsync();
                return;
            }

            var rules = channelRules.Where(x => Matches(x, message)).ToList();
            if (rules.Count == 0)
                return;

            if (!perms.AddReactions || !perms.ReadMessageHistory)
                return;

            foreach (var rule in rules)
                if (!await ReactAsync(channel.Guild, message, rule))
                    break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto reaction failed in {GuildId}", channel.Guild.Id);
        }
    }

    private async Task<bool> ReactAsync(SocketGuild guild, SocketUserMessage message, AutoReaction rule)
    {
        foreach (var emote in ParseEmojis(rule.Emojis))
        {
            var name = emote.ToString() ?? "";
            if (rejected.ContainsKey((rule.Id, name)) || emote is Emote custom && guild.Emotes.All(x => x.Id != custom.Id))
                continue;

            try
            {
                await message.AddReactionAsync(emote);
            }
            catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownEmoji)
            {
                rejected[(rule.Id, name)] = 0;
            }
            catch (HttpException ex) when (ex.DiscordCode is DiscordErrorCode.MissingPermissions
                                               or DiscordErrorCode.InsufficientPermissions
                                               or DiscordErrorCode.ReactionBlocked
                                               or (DiscordErrorCode)30010
                                               or DiscordErrorCode.UnknownMessage
                                               or DiscordErrorCode.UnknownChannel)
            {
                return false;
            }
        }

        return true;
    }
}
