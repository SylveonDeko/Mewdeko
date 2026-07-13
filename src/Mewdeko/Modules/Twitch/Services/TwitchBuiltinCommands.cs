using Mewdeko.Modules.Twitch.Common;

namespace Mewdeko.Modules.Twitch.Services;

/// <summary>
///     Built-in Twitch chat commands available in every configured channel.
///     Additional commands can be added by subclassing <see cref="TwitchModuleBase" /> and
///     decorating methods with <see cref="TwitchCommandAttribute" />.
/// </summary>
public class TwitchBuiltinCommands : TwitchModuleBase
{
    private static readonly DateTime StartTime = DateTime.UtcNow;

    /// <summary>
    ///     Responds with a pong to confirm the bot is alive. Available to all viewers.
    /// </summary>
    [TwitchCommand("ping")]
    public Task Ping()
    {
        return ReplyLocalizedAsync("twitch_cmd_pong");
    }

    /// <summary>
    ///     Reports how long the bot has been running. Available to all viewers.
    /// </summary>
    [TwitchCommand("uptime")]
    public Task Uptime()
    {
        var up = DateTime.UtcNow - StartTime;
        return ReplyLocalizedAsync("twitch_cmd_uptime", (int)up.TotalHours, up.Minutes, up.Seconds);
    }

    /// <summary>
    ///     Shows which Discord account is linked to the caller's Twitch account. Available to all viewers.
    /// </summary>
    [TwitchCommand("discord")]
    public Task Discord()
    {
        if (Context.LinkedDiscordUserId.HasValue)
            return ReplyLocalizedAsync("twitch_cmd_discord_linked", Context.LinkedDiscordUserId.Value);
        else
            return ReplyLocalizedAsync("twitch_cmd_discord_not_linked");
    }

    /// <summary>
    ///     Starts self-service Discord account linking by generating a short claim code.
    /// </summary>
    [TwitchCommand("link")]
    public async Task Link()
    {
        if (Context.LinkedDiscordUserId.HasValue)
        {
            await ReplyLocalizedAsync("twitch_cmd_link_already", Context.LinkedDiscordUserId.Value);
            return;
        }

        var code = await TwitchSvc.GenerateLinkCodeAsync(Context.GuildId, Context.Username);
        await ReplyLocalizedAsync("twitch_cmd_link_code", code);
    }

    /// <summary>
    ///     Shows the currently available Twitch chat commands for this channel.
    /// </summary>
    [TwitchCommand("commands")]
    public async Task Commands()
    {
        var custom = await TwitchSvc.GetCustomCommandsAsync(Context.GuildId);
        var enabledCustom = custom.Where(c => c.Enabled).Select(c => c.Name);
        var names = new[]
            {
                "commands", "counter", "discord", "link", "ping", "quote", "raidtarget", "rank", "schedule", "so",
                "socials", "stream", "uptime"
            }
            .Concat(enabledCustom)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Select(x => $"{Context.CommandPrefix}{x}");

        await ReplyLocalizedAsync("twitch_cmd_commands", string.Join(", ", names));
    }

    /// <summary>
    ///     Shows, adds, or removes saved channel quotes.
    /// </summary>
    [TwitchCommand("quote")]
    public async Task Quote()
    {
        var action = Context.Args.FirstOrDefault();
        if (string.Equals(action, "add", StringComparison.OrdinalIgnoreCase))
        {
            if (Context.PermissionLevel < TwitchPermissionLevel.Mod)
                return;

            var rawText = string.Join(' ', Context.Args.Skip(1)).Trim();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                await ReplyLocalizedAsync("twitch_cmd_quote_add_usage");
                return;
            }

            var author = Context.Args.Length > 2 && Context.Args[^2].Equals("--", StringComparison.Ordinal)
                ? Context.Args[^1]
                : null;
            var text = author is null
                ? rawText
                : string.Join(' ', Context.Args.Skip(1).Take(Context.Args.Length - 3)).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                await ReplyLocalizedAsync("twitch_cmd_quote_add_usage");
                return;
            }

            var quote = await TwitchSvc.AddQuoteAsync(Context.GuildId, text, author, Context.Username);
            await ReplyLocalizedAsync("twitch_cmd_quote_added", quote.Id);
            return;
        }

        if (string.Equals(action, "remove", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "delete", StringComparison.OrdinalIgnoreCase))
        {
            if (Context.PermissionLevel < TwitchPermissionLevel.Mod)
                return;

            if (!int.TryParse(Context.Args.Skip(1).FirstOrDefault(), out var removeId))
            {
                await ReplyLocalizedAsync("twitch_cmd_quote_remove_usage");
                return;
            }

            var removed = await TwitchSvc.RemoveQuoteAsync(Context.GuildId, removeId);
            await ReplyLocalizedAsync(removed ? "twitch_cmd_quote_removed" : "twitch_cmd_quote_not_found", removeId);
            return;
        }

        int? quoteId = null;
        if (int.TryParse(action, out var parsedId))
            quoteId = parsedId;

        var found = await TwitchSvc.GetQuoteAsync(Context.GuildId, quoteId);
        if (found is null)
        {
            await ReplyLocalizedAsync("twitch_cmd_quote_empty");
            return;
        }

        var suffix = string.IsNullOrWhiteSpace(found.Author) ? "" : $" - {found.Author}";
        await SayAsync($"Quote #{found.Id}: \"{found.Text}\"{suffix}");
    }

    /// <summary>
    ///     Shows current stream status for the configured Twitch channel.
    /// </summary>
    [TwitchCommand("stream")]
    public async Task Stream()
    {
        var summary = await TwitchSvc.GetStreamSummaryAsync(Context.GuildId);
        if (string.IsNullOrWhiteSpace(summary))
            await ReplyLocalizedAsync("twitch_cmd_stream_unknown");
        else
            await ReplyAsync(summary);
    }

    /// <summary>
    ///     Shows the configured stream schedule message.
    /// </summary>
    [TwitchCommand("schedule")]
    public async Task Schedule()
    {
        var schedule = await TwitchSvc.GetScheduleMessageAsync(Context.GuildId);
        if (string.IsNullOrWhiteSpace(schedule))
            await ReplyLocalizedAsync("twitch_cmd_schedule_empty");
        else
            await SayAsync(schedule);
    }

    /// <summary>
    ///     Shows the configured social links message.
    /// </summary>
    [TwitchCommand("socials")]
    public async Task Socials()
    {
        var socials = await TwitchSvc.GetSocialsMessageAsync(Context.GuildId);
        if (string.IsNullOrWhiteSpace(socials))
            await ReplyLocalizedAsync("twitch_cmd_socials_empty");
        else
            await SayAsync(socials);
    }

    /// <summary>
    ///     Suggests a configured raid target. Requires moderator permissions.
    /// </summary>
    [TwitchCommand("raidtarget", TwitchCommandPermission.Mod)]
    public async Task RaidTarget()
    {
        var target = await TwitchSvc.GetRandomRaidTargetAsync(Context.GuildId);
        if (target is null)
        {
            await ReplyLocalizedAsync("twitch_cmd_raidtarget_empty");
            return;
        }

        var note = string.IsNullOrWhiteSpace(target.Note) ? "" : $" - {target.Note}";
        await SayAsync($"Raid target: https://twitch.tv/{target.TwitchLogin}{note}");
    }

    /// <summary>
    ///     Shouts out another Twitch channel. Requires moderator permissions.
    /// </summary>
    [TwitchCommand("so", TwitchCommandPermission.Mod)]
    public async Task Shoutout()
    {
        var target = Context.Args.FirstOrDefault()?.TrimStart('@');
        if (string.IsNullOrWhiteSpace(target))
        {
            await ReplyLocalizedAsync("twitch_cmd_so_usage");
            return;
        }

        var shoutout = await TwitchSvc.GetShoutoutAsync(target);
        if (string.IsNullOrWhiteSpace(shoutout))
            await ReplyLocalizedAsync("twitch_cmd_so_not_found", target);
        else
            await SayAsync(shoutout);
    }

    /// <summary>
    ///     Shows or updates a named stream counter.
    /// </summary>
    [TwitchCommand("counter")]
    public async Task Counter()
    {
        var name = Context.Args.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(name))
        {
            await ReplyLocalizedAsync("twitch_cmd_counter_usage");
            return;
        }

        var operation = Context.Args.Skip(1).FirstOrDefault();
        if (operation is null)
        {
            var current = await TwitchSvc.GetCounterAsync(Context.GuildId, name) ?? 0;
            await ReplyLocalizedAsync("twitch_cmd_counter_value", name, current);
            return;
        }

        if (Context.PermissionLevel < TwitchPermissionLevel.Mod)
            return;

        var value = operation switch
        {
            "+" or "++" or "add" => await TwitchSvc.AddCounterAsync(Context.GuildId, name, 1),
            "-" or "--" or "sub" => await TwitchSvc.AddCounterAsync(Context.GuildId, name, -1),
            "reset" => await TwitchSvc.SetCounterAsync(Context.GuildId, name, 0),
            _ when int.TryParse(operation, out var parsed) => await TwitchSvc.SetCounterAsync(Context.GuildId, name,
                parsed),
            _ => int.MinValue
        };

        if (value == int.MinValue)
        {
            await ReplyLocalizedAsync("twitch_cmd_counter_usage");
            return;
        }

        await ReplyLocalizedAsync("twitch_cmd_counter_value", name, value);
    }

    /// <summary>
    ///     Shows the caller's resolved permission level in the channel. Available to all viewers.
    /// </summary>
    [TwitchCommand("rank")]
    public Task Rank()
    {
        var rankKey = Context.PermissionLevel switch
        {
            TwitchPermissionLevel.Broadcaster => "twitch_cmd_rank_broadcaster",
            TwitchPermissionLevel.Mod => "twitch_cmd_rank_mod",
            TwitchPermissionLevel.Vip => "twitch_cmd_rank_vip",
            TwitchPermissionLevel.Subscriber => "twitch_cmd_rank_subscriber",
            _ => "twitch_cmd_rank_viewer"
        };

        return ReplyLocalizedAsync("twitch_cmd_rank", GetText(rankKey));
    }
}