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
        ReplyLocalized("twitch_cmd_pong");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Reports how long the bot has been running. Available to all viewers.
    /// </summary>
    [TwitchCommand("uptime")]
    public Task Uptime()
    {
        var up = DateTime.UtcNow - StartTime;
        ReplyLocalized("twitch_cmd_uptime", (int)up.TotalHours, up.Minutes, up.Seconds);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Shows which Discord account is linked to the caller's Twitch account. Available to all viewers.
    /// </summary>
    [TwitchCommand("discord")]
    public Task Discord()
    {
        if (Context.LinkedDiscordUserId.HasValue)
            ReplyLocalized("twitch_cmd_discord_linked", Context.LinkedDiscordUserId.Value);
        else
            ReplyLocalized("twitch_cmd_discord_not_linked");

        return Task.CompletedTask;
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

        ReplyLocalized("twitch_cmd_rank", GetText(rankKey));
        return Task.CompletedTask;
    }
}