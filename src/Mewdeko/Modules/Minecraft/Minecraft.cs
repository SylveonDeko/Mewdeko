using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Minecraft.Common;
using Mewdeko.Modules.Minecraft.Services;

namespace Mewdeko.Modules.Minecraft;

/// <summary>
///     Module for Minecraft server management and status queries.
/// </summary>
public class Minecraft : MewdekoModuleBase<MinecraftService>
{
    /// <summary>
    ///     Queries the status of a registered server or a direct address.
    /// </summary>
    /// <param name="serverName">The registered server name or direct address. Uses the default server if omitted.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task McStatus([Remainder] string? serverName = null)
    {
        if (!await Service.CheckQueryRateLimitAsync(ctx.Guild.Id))
        {
            await ErrorAsync(Strings.McRateLimited(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var server = await Service.GetServerAsync(ctx.Guild.Id, serverName);

        if (server == null && !string.IsNullOrWhiteSpace(serverName) && serverName.Contains('.'))
        {
            var parts = serverName.Split(':');
            var address = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 25565;

            if (!Service.IsAddressSafe(address))
            {
                await ErrorAsync(Strings.McAddressBlocked(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var status = await Service.QueryJavaServerAsync(address, port);
            if (status == null)
            {
                await ErrorAsync(Strings.McQueryFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var tempServer = new MinecraftServer
            {
                Name = address, Address = address, Port = port
            };
            await ctx.Channel.SendMessageAsync(embed: Service.BuildStatusEmbed(tempServer, status, ctx.Guild.Id))
                .ConfigureAwait(false);
            return;
        }

        if (server == null)
        {
            await ErrorAsync(Strings.McServerNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var serverStatus = await Service.QueryServerAsync(server);
        if (serverStatus == null)
        {
            await ErrorAsync(Strings.McQueryFailed(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.SendMessageAsync(embed: Service.BuildStatusEmbed(server, serverStatus, ctx.Guild.Id))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a Minecraft server to the guild's server list.
    /// </summary>
    /// <param name="name">The label for the server.</param>
    /// <param name="address">The server address, optionally with port (e.g. play.example.com:25565).</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task McAdd(string name, [Remainder] string address)
    {
        var serverType = McServerType.Java;
        var port = 25565;

        if (address.StartsWith("bedrock:", StringComparison.OrdinalIgnoreCase))
        {
            serverType = McServerType.Bedrock;
            address = address[8..];
            port = 19132;
        }

        var addrParts = address.Split(':');
        var host = addrParts[0];
        if (addrParts.Length > 1 && int.TryParse(addrParts[1], out var customPort))
            port = customPort;

        try
        {
            var server = await Service.AddServerAsync(ctx.Guild.Id, name, host, port, serverType);
            await ConfirmAsync(Strings.McServerAdded(ctx.Guild.Id, server.Name, server.Address, server.Port))
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            await ErrorAsync(Strings.McServerExists(ctx.Guild.Id, name)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes a Minecraft server from the guild's server list.
    /// </summary>
    /// <param name="name">The name of the server to remove.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task McRemove([Remainder] string name)
    {
        var removed = await Service.RemoveServerAsync(ctx.Guild.Id, name);
        if (removed)
            await ConfirmAsync(Strings.McServerRemoved(ctx.Guild.Id, name)).ConfigureAwait(false);
        else
            await ErrorAsync(Strings.McServerNotFound(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all registered Minecraft servers for this guild.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task McList()
    {
        var servers = await Service.GetServersAsync(ctx.Guild.Id);
        if (servers.Count == 0)
        {
            await ErrorAsync(Strings.McNoServers(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.McServerListTitle(ctx.Guild.Id));

        foreach (var s in servers)
        {
            var type = (McServerType)s.ServerType == McServerType.Bedrock ? "Bedrock" : "Java";
            var def = s.IsDefault ? " ⭐" : "";
            var watch = s.WatchChannelId.HasValue ? $" 📡 <#{s.WatchChannelId}>" : "";
            eb.AddField($"{s.Name}{def}", $"`{s.Address}:{s.Port}` ({type}){watch}", true);
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a server as the default for status commands.
    /// </summary>
    /// <param name="name">The name of the server to set as default.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task McDefault([Remainder] string name)
    {
        var success = await Service.SetDefaultServerAsync(ctx.Guild.Id, name);
        if (success)
            await ConfirmAsync(Strings.McDefaultSet(ctx.Guild.Id, name)).ConfigureAwait(false);
        else
            await ErrorAsync(Strings.McServerNotFound(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a channel to receive periodic status updates for a server.
    /// </summary>
    /// <param name="serverName">The name of the server to watch.</param>
    /// <param name="channel">The channel to post updates in, or omit to disable.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task McWatch(string serverName, ITextChannel? channel = null)
    {
        var server = await Service.SetWatchChannelAsync(ctx.Guild.Id, serverName, channel?.Id);
        if (server == null)
        {
            await ErrorAsync(Strings.McServerNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (channel == null)
            await ConfirmAsync(Strings.McWatchDisabled(ctx.Guild.Id, serverName)).ConfigureAwait(false);
        else
            await ConfirmAsync(Strings.McWatchEnabled(ctx.Guild.Id, serverName, channel.Mention))
                .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets how server status is displayed: embed, channel topic, or both.
    /// </summary>
    /// <param name="serverName">The name of the server.</param>
    /// <param name="mode">The watch mode (embed, topic, both).</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task McWatchMode(string serverName, McWatchMode mode)
    {
        var server = await Service.SetWatchModeAsync(ctx.Guild.Id, serverName, mode);
        if (server == null)
        {
            await ErrorAsync(Strings.McServerNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ConfirmAsync(Strings.McWatchModeSet(ctx.Guild.Id, serverName, mode.ToString()))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a custom embed template for a watched server's status messages.
    /// </summary>
    /// <param name="serverName">The name of the server.</param>
    /// <param name="template">The embed template JSON, or "-" to reset to default.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task McEmbed(string serverName, [Remainder] string? template = null)
    {
        var effectiveTemplate = template == "-" ? null : template;
        var server = await Service.SetCustomEmbedAsync(ctx.Guild.Id, serverName, effectiveTemplate);
        if (server == null)
        {
            await ErrorAsync(Strings.McServerNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (effectiveTemplate == null)
            await ConfirmAsync(Strings.McEmbedReset(ctx.Guild.Id, serverName)).ConfigureAwait(false);
        else
            await ConfirmAsync(Strings.McEmbedSet(ctx.Guild.Id, serverName)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Looks up a Minecraft player by username and displays their profile and skin.
    /// </summary>
    /// <param name="username">The player's Minecraft username.</param>
    [Cmd]
    [Aliases]
    public async Task McPlayer([Remainder] string username)
    {
        var profile = await Service.LookupPlayerAsync(username);
        if (profile == null)
        {
            await ErrorAsync(Strings.McPlayerNotFound(ctx.Guild.Id, username)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(profile.Username)
            .WithThumbnailUrl(profile.AvatarUrl)
            .WithImageUrl(profile.SkinUrl)
            .AddField("UUID", $"`{profile.Uuid}`");

        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays a Minecraft player's skin render.
    /// </summary>
    /// <param name="username">The player's Minecraft username.</param>
    [Cmd]
    [Aliases]
    public async Task McSkin([Remainder] string username)
    {
        var profile = await Service.LookupPlayerAsync(username);
        if (profile == null)
        {
            await ErrorAsync(Strings.McPlayerNotFound(ctx.Guild.Id, username)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.McSkinTitle(ctx.Guild.Id, profile.Username))
            .WithImageUrl(profile.SkinUrl);

        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sends an RCON command to a registered Minecraft server.
    /// </summary>
    /// <param name="serverName">The name of the server.</param>
    /// <param name="command">The command to execute.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task McRcon(string serverName, [Remainder] string command)
    {
        var (success, response, _) = await Service.SendRconCommandAsync(ctx.Guild.Id, serverName, command);
        if (success)
            await ConfirmAsync($"```\n{response}\n```").ConfigureAwait(false);
        else
            await ErrorAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    ///     Manages the whitelist on a registered Minecraft server via RCON.
    /// </summary>
    /// <param name="serverName">The server name.</param>
    /// <param name="action">The action: add, remove, list, on, off, reload.</param>
    /// <param name="playerName">The player name (for add/remove).</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task McWhitelist(string serverName, string action, [Remainder] string? playerName = null)
    {
        var (success, response) = await Service.WhitelistCommandAsync(ctx.Guild.Id, serverName, action, playerName);

        if (!success)
        {
            await ErrorAsync(response).ConfigureAwait(false);
            return;
        }

        if (action.Equals("list", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(response))
        {
            var players = response.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(p => !p.StartsWith("There are"))
                .ToList();

            if (players.Count > 0)
            {
                var eb = new EmbedBuilder().WithOkColor()
                    .WithTitle(Strings.McWhitelistTitle(ctx.Guild.Id, serverName));

                var descriptions = new List<string>();
                foreach (var player in players)
                {
                    var avatarUrl = $"https://crafatar.com/avatars/{player}?size=16&overlay";
                    descriptions.Add(player);
                }

                eb.WithDescription(string.Join("\n", descriptions));
                eb.WithFooter($"{players.Count} player(s)");
                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                return;
            }
        }

        await ConfirmAsync($"```\n{response}\n```").ConfigureAwait(false);
    }
}