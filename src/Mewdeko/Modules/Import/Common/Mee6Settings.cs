using System.Text.Json;

namespace Mewdeko.Modules.Import.Common;

/// <summary>
///     The parts of a MEE6 server that can be brought over. Values are the keys clients send back to choose them.
/// </summary>
public static class Mee6Section
{
    /// <summary>Welcome and goodbye messages and join roles.</summary>
    public const string Welcome = "welcome";

    /// <summary>Level-up announcements, level roles, XP rate and channels or roles that earn no XP.</summary>
    public const string Levels = "levels";

    /// <summary>The birthday channel, role and message.</summary>
    public const string Birthdays = "birthdays";

    /// <summary>Custom commands, brought over as chat triggers.</summary>
    public const string Commands = "commands";

    /// <summary>Reaction roles and button role menus.</summary>
    public const string ReactionRoles = "reactionRoles";

    /// <summary>The word, invite and link filters, spam and mention limits and automatic punishments.</summary>
    public const string AutoMod = "automod";

    /// <summary>Twitch live announcements.</summary>
    public const string Twitch = "twitch";

    /// <summary>The currency emoji and shop items.</summary>
    public const string Economy = "economy";

    /// <summary>Every section, in the order they are shown.</summary>
    public static readonly string[] All = [Welcome, Levels, Birthdays, Commands, ReactionRoles, AutoMod, Twitch, Economy];
}

/// <summary>
///     Welcome and goodbye settings read from MEE6.
/// </summary>
public sealed class Mee6WelcomePlan
{
    /// <summary>The channel welcome messages go to, when they are on.</summary>
    public ulong? ChannelId { get; init; }

    /// <summary>The welcome message.</summary>
    public string? ChannelMessage { get; init; }

    /// <summary>The private welcome message, when it is on.</summary>
    public string? DmMessage { get; init; }

    /// <summary>The goodbye channel, when goodbyes are on.</summary>
    public ulong? ByeChannelId { get; init; }

    /// <summary>The goodbye message.</summary>
    public string? ByeMessage { get; init; }

    /// <summary>Roles given to new members.</summary>
    public List<ulong> JoinRoles { get; init; } = [];
}

/// <summary>
///     Level settings read from MEE6.
/// </summary>
public sealed class Mee6LevelsPlan
{
    /// <summary>0 off, 1 the channel the member spoke in, 2 a DM, 3 a set channel.</summary>
    public int AnnouncementType { get; init; }

    /// <summary>The set announcement channel.</summary>
    public ulong? AnnouncementChannelId { get; init; }

    /// <summary>The level-up message.</summary>
    public string? Message { get; init; }

    /// <summary>Channels that earn no XP.</summary>
    public List<ulong> ExcludedChannels { get; init; } = [];

    /// <summary>Roles that earn no XP.</summary>
    public List<ulong> ExcludedRoles { get; init; } = [];

    /// <summary>The XP multiplier.</summary>
    public double XpRate { get; init; } = 1;

    /// <summary>Whether a new level role replaces the previous one.</summary>
    public bool RemovePreviousRewards { get; init; }

    /// <summary>Level roles.</summary>
    public List<ImportedRoleReward> RoleRewards { get; init; } = [];
}

/// <summary>
///     Birthday settings read from MEE6.
/// </summary>
public sealed class Mee6BirthdayPlan
{
    /// <summary>Where birthday wishes go.</summary>
    public ulong? ChannelId { get; init; }

    /// <summary>The role given on a member's birthday.</summary>
    public ulong? RoleId { get; init; }

    /// <summary>The birthday wish.</summary>
    public string? Message { get; init; }
}

/// <summary>
///     A MEE6 custom command.
/// </summary>
public sealed class Mee6CommandPlan
{
    /// <summary>The command name, used after <c>!</c>.</summary>
    public string Name { get; init; } = "";

    /// <summary>The first response.</summary>
    public string Response { get; init; } = "";

    /// <summary>Other responses, picked at random.</summary>
    public List<string> MoreResponses { get; init; } = [];

    /// <summary>Whether the reply is sent in a DM.</summary>
    public bool Private { get; init; }

    /// <summary>Roles the command gives.</summary>
    public List<ulong> GrantedRoles { get; init; } = [];

    /// <summary>Whether the command was turned off in MEE6.</summary>
    public bool Disabled { get; init; }
}

/// <summary>
///     A MEE6 reaction role or button role message.
/// </summary>
public sealed class Mee6RoleMessagePlan
{
    /// <summary>The message's name in MEE6.</summary>
    public string Name { get; init; } = "";

    /// <summary>Whether members react, rather than press buttons.</summary>
    public bool IsReaction { get; init; }

    /// <summary>The channel the message is in.</summary>
    public ulong ChannelId { get; init; }

    /// <summary>The posted message, for reaction roles that keep working on it.</summary>
    public ulong MessageId { get; init; }

    /// <summary>The message text, for button menus that are posted again.</summary>
    public string? Message { get; init; }

    /// <summary>Whether members can hold only one of the roles.</summary>
    public bool Single { get; init; }

    /// <summary>The emoji or button for each set of roles.</summary>
    public List<Mee6RoleOption> Options { get; init; } = [];
}

/// <summary>
///     One reaction or button on a MEE6 role message.
/// </summary>
public sealed class Mee6RoleOption
{
    /// <summary>A unicode emoji.</summary>
    public string? EmojiName { get; init; }

    /// <summary>A custom emoji's ID.</summary>
    public ulong? EmojiId { get; init; }

    /// <summary>The button text.</summary>
    public string? Label { get; init; }

    /// <summary>The Discord button style.</summary>
    public int ButtonStyle { get; init; } = 2;

    /// <summary>The roles it gives.</summary>
    public List<ulong> Roles { get; init; } = [];
}

/// <summary>
///     Auto-moderation settings read from MEE6.
/// </summary>
public sealed class Mee6AutoModPlan
{
    /// <summary>Filtered words, when the word filter is on.</summary>
    public List<string> BadWords { get; init; } = [];

    /// <summary>Whether a filtered word also warns the member.</summary>
    public bool WarnOnWords { get; init; }

    /// <summary>Whether invites are filtered.</summary>
    public bool FilterInvites { get; init; }

    /// <summary>Whether an invite also warns the member.</summary>
    public bool WarnOnInvites { get; init; }

    /// <summary>Whether links are filtered.</summary>
    public bool FilterLinks { get; init; }

    /// <summary>Messages within the spam window that count as spam, or 0 when off.</summary>
    public int SpamThreshold { get; init; }

    /// <summary>Mentions in one message that count as mass mentioning, or 0 when off.</summary>
    public int MentionThreshold { get; init; }

    /// <summary>Punishments by warning count.</summary>
    public List<Mee6Punishment> Punishments { get; init; } = [];
}

/// <summary>
///     A MEE6 automated action, read as a warning punishment.
/// </summary>
/// <param name="Warnings">Warnings that trigger it.</param>
/// <param name="Action">mute, tempmute, kick, ban or tempban.</param>
/// <param name="Minutes">How long, for timed punishments.</param>
public sealed record Mee6Punishment(int Warnings, string Action, int Minutes);

/// <summary>
///     A Twitch streamer MEE6 announces.
/// </summary>
/// <param name="Login">The streamer's Twitch name.</param>
/// <param name="ChannelId">Where announcements go.</param>
/// <param name="Message">The announcement.</param>
public sealed record Mee6StreamPlan(string Login, ulong ChannelId, string Message);

/// <summary>
///     Economy settings read from MEE6.
/// </summary>
public sealed class Mee6EconomyPlan
{
    /// <summary>The currency's custom emoji ID.</summary>
    public ulong? CurrencyEmojiId { get; init; }

    /// <summary>The currency's name, shown in the preview only since Mewdeko names currency by emoji.</summary>
    public string? CurrencyName { get; init; }

    /// <summary>Shop items.</summary>
    public List<Mee6ShopItem> Items { get; init; } = [];
}

/// <summary>
///     A MEE6 shop item.
/// </summary>
/// <param name="Name">The item name.</param>
/// <param name="Description">The item description.</param>
/// <param name="Price">The price.</param>
/// <param name="RoleId">The role it gives, if any.</param>
public sealed record Mee6ShopItem(string Name, string? Description, long Price, ulong? RoleId);

/// <summary>
///     Everything read from a MEE6 settings export.
/// </summary>
public sealed class Mee6SettingsPlan
{
    /// <summary>The server the export came from.</summary>
    public ulong GuildId { get; init; }

    /// <summary>Welcome settings, when the plugin is on.</summary>
    public Mee6WelcomePlan? Welcome { get; init; }

    /// <summary>Level settings, when the plugin is on.</summary>
    public Mee6LevelsPlan? Levels { get; init; }

    /// <summary>Birthday settings, when the plugin is on.</summary>
    public Mee6BirthdayPlan? Birthdays { get; init; }

    /// <summary>Custom commands.</summary>
    public List<Mee6CommandPlan> Commands { get; init; } = [];

    /// <summary>Published reaction role and button role messages.</summary>
    public List<Mee6RoleMessagePlan> RoleMessages { get; init; } = [];

    /// <summary>Auto-moderation, when the plugin is on.</summary>
    public Mee6AutoModPlan? AutoMod { get; init; }

    /// <summary>Twitch streamers.</summary>
    public List<Mee6StreamPlan> Streams { get; init; } = [];

    /// <summary>Economy settings, when the plugin is on.</summary>
    public Mee6EconomyPlan? Economy { get; init; }

    /// <summary>The sections that have something to bring over.</summary>
    public IEnumerable<string> AvailableSections()
    {
        if (Welcome is not null) yield return Mee6Section.Welcome;
        if (Levels is not null) yield return Mee6Section.Levels;
        if (Birthdays is not null) yield return Mee6Section.Birthdays;
        if (Commands.Count > 0) yield return Mee6Section.Commands;
        if (RoleMessages.Count > 0) yield return Mee6Section.ReactionRoles;
        if (AutoMod is not null) yield return Mee6Section.AutoMod;
        if (Streams.Count > 0) yield return Mee6Section.Twitch;
        if (Economy is not null) yield return Mee6Section.Economy;
    }
}

/// <summary>
///     Reads the file the MEE6 export script downloads: every response from MEE6's dashboard API keyed by path.
/// </summary>
public static class Mee6SettingsReader
{
    /// <summary>
    ///     Whether a file looks like a MEE6 settings export.
    /// </summary>
    /// <param name="content">The file's text.</param>
    public static bool IsExport(string content)
    {
        var start = content.AsSpan().TrimStart();
        return start.Length > 0 && start[0] == '{' && content.Contains("\"responses\"") &&
               content.Contains("plugins/");
    }

    /// <summary>
    ///     Reads a MEE6 settings export.
    /// </summary>
    /// <param name="content">The file's text.</param>
    /// <exception cref="ImportException">The file is not a MEE6 export.</exception>
    public static Mee6SettingsPlan Read(string content)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            throw new ImportException(ImportError.UnreadableFile);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("responses", out var responses) ||
                !ulong.TryParse(Text(root, "guildId"), out var guildId))
                throw new ImportException(ImportError.UnreadableFile);

            JsonElement? Body(string path)
            {
                if (!responses.TryGetProperty(path, out var entry) ||
                    !entry.TryGetProperty("status", out var status) || status.GetInt32() != 200 ||
                    !entry.TryGetProperty("body", out var body))
                    return null;
                return body.Clone();
            }

            JsonElement? Config(string plugin)
            {
                var body = Body($"plugins/{plugin}/config/{guildId}");
                return body is { ValueKind: JsonValueKind.Object } b && Bool(b, "enabled") ? b : null;
            }

            return new Mee6SettingsPlan
            {
                GuildId = guildId,
                Welcome = Config("welcome") is { } welcome ? ReadWelcome(welcome) : null,
                Levels = Config("levels") is { } levels ? ReadLevels(levels) : null,
                Birthdays = Config("birthdays") is { } birthdays ? ReadBirthdays(birthdays) : null,
                Commands = Body($"plugins/commands/guilds/{guildId}/commands") is { } commands
                    ? ReadCommands(commands)
                    : [],
                RoleMessages = Body($"plugins/reaction_roles/guilds/{guildId}/messages") is { } roles
                    ? ReadRoleMessages(roles)
                    : [],
                AutoMod = Config("moderator") is { } moderator ? ReadAutoMod(moderator) : null,
                Streams = Config("twitch") is not null &&
                          Body($"plugins/twitch/guilds/{guildId}/streamers") is { } streamers
                    ? ReadStreams(streamers)
                    : [],
                Economy = Config("economy") is { } economy
                    ? ReadEconomy(economy, Body($"plugins/economy/guilds/{guildId}/wares"))
                    : null
            };
        }
    }

    private static Mee6WelcomePlan ReadWelcome(JsonElement config)
    {
        string Message(string prefix)
        {
            config.TryGetProperty($"{prefix}_embed", out var embed);
            return Mee6Text.BuildMessage(Text(config, $"{prefix}_message"), embed, Bool(config, $"{prefix}_in_embed"));
        }

        return new Mee6WelcomePlan
        {
            ChannelId = Bool(config, "public_welcome_enabled") ? Id(config, "public_welcome_channel_id") : null,
            ChannelMessage = Bool(config, "public_welcome_enabled") ? Message("public_welcome") : null,
            DmMessage = Bool(config, "private_welcome_enabled") ? Message("private_welcome") : null,
            ByeChannelId = Bool(config, "goodbye_enabled") ? Id(config, "goodbye_channel_id") : null,
            ByeMessage = Bool(config, "goodbye_enabled") ? Mee6Text.Translate(Text(config, "goodbye_message")) : null,
            JoinRoles = Bool(config, "roles_enabled") ? Ids(config, "roles") : []
        };
    }

    private static Mee6LevelsPlan ReadLevels(JsonElement config)
    {
        var excludedChannels = Ids(config, "banned_channels");
        if (config.TryGetProperty("channels_permissions", out var channels) &&
            Text(channels, "default_behavior") == "allow_all")
            excludedChannels = excludedChannels.Union(Ids(channels, "exclude")).ToList();

        var excludedRoles = Ids(config, "banned_roles");
        if (config.TryGetProperty("roles_permissions", out var roles) && Text(roles, "default_behavior") == "allow_all")
            excludedRoles = excludedRoles.Union(Ids(roles, "exclude")).ToList();

        var rewards = new List<ImportedRoleReward>();
        if (config.TryGetProperty("role_rewards", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var reward in list.EnumerateArray())
            {
                if (reward.TryGetProperty("rank", out var rank) && rank.TryGetInt32(out var level) &&
                    Id(reward, "role") is { } roleId)
                    rewards.Add(new ImportedRoleReward(level, roleId));
            }
        }

        return new Mee6LevelsPlan
        {
            AnnouncementType = Int(config, "level_up_announcement_type"),
            AnnouncementChannelId = Id(config, "level_up_announcement_channel"),
            Message = Mee6Text.Translate(Text(config, "level_up_announcement_message"), Mee6TextContext.LevelUp),
            ExcludedChannels = excludedChannels,
            ExcludedRoles = excludedRoles,
            XpRate = config.TryGetProperty("xp_rate", out var rate) && rate.TryGetDouble(out var r) && r > 0 ? r : 1,
            RemovePreviousRewards = Int(config, "role_rewards_type") == 1,
            RoleRewards = rewards
        };
    }

    private static Mee6BirthdayPlan ReadBirthdays(JsonElement config)
    {
        return new Mee6BirthdayPlan
        {
            ChannelId = Id(config, "wish_channel_id"),
            RoleId = Ids(config, "birthday_roles").FirstOrDefault() is var role and > 0 ? role : null,
            Message = Mee6Text.Translate(Text(config, "wish_message"), Mee6TextContext.Birthday)
        };
    }

    private static List<Mee6CommandPlan> ReadCommands(JsonElement body)
    {
        var list = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("commands", out var commands)
            ? commands
            : body;
        if (list.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<Mee6CommandPlan>();
        foreach (var command in list.EnumerateArray())
        {
            var name = Text(command, "id");
            if (string.IsNullOrWhiteSpace(name) || !command.TryGetProperty("actions", out var actions))
                continue;

            var responses = new List<string>();
            var granted = new List<ulong>();
            var isPrivate = false;
            foreach (var action in actions.EnumerateArray())
            {
                var type = Text(action, "type");
                if (type is "respond" or "send" && action.TryGetProperty("messages", out var messages))
                {
                    isPrivate |= type == "respond" && Bool(action, "direct_response");
                    responses.AddRange(messages.EnumerateArray()
                        .Select(x => Mee6Text.BuildFromMessageObject(x))
                        .Where(x => x.Length > 0));
                }
                else if (type == "add_roles")
                {
                    granted.AddRange(Ids(action, "roles"));
                    if (action.TryGetProperty("messages", out var roleMessages))
                        responses.AddRange(roleMessages.EnumerateArray()
                            .Select(x => Mee6Text.BuildFromMessageObject(x))
                            .Where(x => x.Length > 0));
                }
            }

            if (responses.Count == 0 && granted.Count == 0)
                continue;

            result.Add(new Mee6CommandPlan
            {
                Name = name.Trim().ToLowerInvariant(),
                Response = responses.FirstOrDefault() ?? "",
                MoreResponses = responses.Skip(1).ToList(),
                Private = isPrivate,
                GrantedRoles = granted.Distinct().ToList(),
                Disabled = command.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.False
            });
        }

        return result;
    }

    private static List<Mee6RoleMessagePlan> ReadRoleMessages(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<Mee6RoleMessagePlan>();
        foreach (var message in body.EnumerateArray())
        {
            if (Text(message, "status") != "published" || Id(message, "channel_id") is not { } channelId)
                continue;

            var kind = Text(message, "kind");
            var options = new List<Mee6RoleOption>();
            if (kind == "reaction" && message.TryGetProperty("reactions", out var reactions))
            {
                options.AddRange(reactions.EnumerateArray().Select(x => new Mee6RoleOption
                {
                    EmojiId = Id(x, "emoji_id"), EmojiName = Text(x, "emoji_name"), Roles = Ids(x, "roles")
                }));
            }
            else if (kind == "button" && message.TryGetProperty("buttons", out var buttons) &&
                     buttons.ValueKind == JsonValueKind.Array)
            {
                foreach (var button in buttons.EnumerateArray())
                {
                    ulong? emojiId = null;
                    string? emojiName = null;
                    if (button.TryGetProperty("emoji", out var emoji) && emoji.ValueKind == JsonValueKind.Object)
                    {
                        emojiId = Id(emoji, "id");
                        emojiName = Text(emoji, "name");
                    }

                    options.Add(new Mee6RoleOption
                    {
                        EmojiId = emojiId,
                        EmojiName = emojiName,
                        Label = Text(button, "label"),
                        ButtonStyle = Math.Clamp(Int(button, "style") is var s and > 0 ? s : 2, 1, 4),
                        Roles = Ids(button, "roles")
                    });
                }
            }
            else
            {
                continue;
            }

            options = options.Where(x => x.Roles.Count > 0).ToList();
            if (options.Count == 0)
                continue;

            message.TryGetProperty("embed", out var embed);
            result.Add(new Mee6RoleMessagePlan
            {
                Name = Text(message, "name") ?? "Roles",
                IsReaction = kind == "reaction",
                ChannelId = channelId,
                MessageId = Id(message, "message_id") ?? 0,
                Message = Mee6Text.BuildMessage(Text(message, "message_content"), embed, Bool(message, "in_embed")),
                Single = Bool(message, "single"),
                Options = options
            });
        }

        return result;
    }

    private static Mee6AutoModPlan ReadAutoMod(JsonElement config)
    {
        int Sanction(string check)
        {
            return config.TryGetProperty(check, out var c) && c.ValueKind == JsonValueKind.Object
                ? Int(c, "sanction")
                : 0;
        }

        int Threshold(string check)
        {
            return Sanction(check) > 0 && config.TryGetProperty(check, out var c) ? Int(c, "threshold") : 0;
        }

        var words = Sanction("check_bad_words") > 0 && config.TryGetProperty("check_bad_words", out var badWords) &&
                    badWords.TryGetProperty("bad_words", out var list)
            ? list.EnumerateArray().Select(x => x.GetString()?.Trim().ToLowerInvariant())
                .Where(x => !string.IsNullOrEmpty(x)).Distinct().Cast<string>().ToList()
            : [];

        var punishments = new List<Mee6Punishment>();
        if (config.TryGetProperty("automated_actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var action in actions.EnumerateArray())
            {
                var threshold = Int(action, "threshold");
                var type = Text(action, "type");
                if (threshold > 0 && type is not null)
                    punishments.Add(new Mee6Punishment(threshold, type, Int(action, "duration") / 60));
            }
        }

        return new Mee6AutoModPlan
        {
            BadWords = words,
            WarnOnWords = Sanction("check_bad_words") >= 2,
            FilterInvites = Sanction("check_invites") > 0,
            WarnOnInvites = Sanction("check_invites") >= 2,
            FilterLinks = Sanction("check_links") > 0,
            SpamThreshold = Threshold("check_fast_messages"),
            MentionThreshold = Threshold("check_mass_mentions"),
            Punishments = punishments.DistinctBy(x => x.Warnings).ToList()
        };
    }

    private static List<Mee6StreamPlan> ReadStreams(JsonElement body)
    {
        var list = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("streamers", out var streamers)
            ? streamers
            : body;
        if (list.ValueKind != JsonValueKind.Array)
            return [];

        return list.EnumerateArray()
            .Where(x => !x.TryGetProperty("enabled", out var e) || e.ValueKind != JsonValueKind.False)
            .Select(x => (Login: (Text(x, "name") ?? Text(x, "display_name"))?.Trim().ToLowerInvariant(),
                Channel: Id(x, "announcement_channel_id"), Message: Text(x, "announcement_message")))
            .Where(x => !string.IsNullOrEmpty(x.Login) && x.Channel is not null)
            .Select(x => new Mee6StreamPlan(x.Login!, x.Channel!.Value,
                Mee6Text.Translate(x.Message, Mee6TextContext.Stream)))
            .DistinctBy(x => x.Login)
            .ToList();
    }

    private static Mee6EconomyPlan ReadEconomy(JsonElement config, JsonElement? wares)
    {
        var items = new List<Mee6ShopItem>();
        if (wares is { ValueKind: JsonValueKind.Array } list)
        {
            foreach (var ware in list.EnumerateArray())
            {
                if (Text(ware, "name") is not { } name)
                    continue;
                items.Add(new Mee6ShopItem(name, Text(ware, "description"),
                    ware.TryGetProperty("price", out var price) && price.TryGetInt64(out var p) ? p : 0,
                    Id(ware, "role_id")));
            }
        }

        return new Mee6EconomyPlan
        {
            CurrencyEmojiId = Id(config, "currency_icon"), CurrencyName = Text(config, "currency_name"), Items = items
        };
    }

    private static string? Text(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool Bool(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.True;
    }

    private static int Int(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : 0;
    }

    private static ulong? Id(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String when ulong.TryParse(value.GetString(), out var id) && id > 0 => id,
            JsonValueKind.Number when value.TryGetUInt64(out var number) && number > 0 => number,
            _ => null
        };
    }

    private static List<ulong> Ids(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Array)
            return [];
        return value.EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.String && ulong.TryParse(x.GetString(), out var id) ? id : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }
}
