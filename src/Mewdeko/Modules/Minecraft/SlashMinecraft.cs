using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Minecraft.Common;
using Mewdeko.Modules.Minecraft.Services;

namespace Mewdeko.Modules.Minecraft;

/// <summary>
///     Slash command module for Minecraft server management and status queries.
/// </summary>
[Group("minecraft", "Minecraft server management and status")]
public class SlashMinecraft : MewdekoSlashModuleBase<MinecraftService>
{
    /// <summary>
    ///     Queries the status of a registered server or a direct address.
    /// </summary>
    /// <param name="serverName">The registered server name or direct address. Uses the default server if omitted.</param>
    [SlashCommand("status", "Check the status of a Minecraft server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task McStatus(string? serverName = null)
    {
        await DeferAsync().ConfigureAwait(false);

        var server = await Service.GetServerAsync(ctx.Guild.Id, serverName);

        if (server == null && !string.IsNullOrWhiteSpace(serverName) && serverName.Contains('.'))
        {
            var parts = serverName.Split(':');
            var address = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 25565;
            var status = await Service.QueryJavaServerAsync(address, port);
            if (status == null)
            {
                await FollowupAsync(embed: new EmbedBuilder().WithErrorColor()
                    .WithDescription(Strings.McQueryFailed(ctx.Guild.Id)).Build()).ConfigureAwait(false);
                return;
            }

            var tempServer = new MinecraftServer
            {
                Name = address, Address = address, Port = port
            };
            await FollowupAsync(embed: Service.BuildStatusEmbed(tempServer, status, ctx.Guild.Id))
                .ConfigureAwait(false);
            return;
        }

        if (server == null)
        {
            await FollowupAsync(embed: new EmbedBuilder().WithErrorColor()
                .WithDescription(Strings.McServerNotFound(ctx.Guild.Id)).Build()).ConfigureAwait(false);
            return;
        }

        var serverStatus = await Service.QueryServerAsync(server);
        if (serverStatus == null)
        {
            await FollowupAsync(embed: new EmbedBuilder().WithErrorColor()
                .WithDescription(Strings.McQueryFailed(ctx.Guild.Id)).Build()).ConfigureAwait(false);
            return;
        }

        await FollowupAsync(embed: Service.BuildStatusEmbed(server, serverStatus, ctx.Guild.Id))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a Minecraft server to the guild's server list.
    /// </summary>
    /// <param name="name">The label for the server.</param>
    /// <param name="address">The server address (e.g. play.example.com or play.example.com:25565).</param>
    /// <param name="type">The server type.</param>
    [SlashCommand("add", "Add a Minecraft server to monitor")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task McAdd(string name, string address,
        [Choice("Java", 0)] [Choice("Bedrock", 1)]
        int type = 0)
    {
        var serverType = (McServerType)type;
        var port = serverType == McServerType.Bedrock ? 19132 : 25565;

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
    [SlashCommand("remove", "Remove a Minecraft server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task McRemove(string name)
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
    [SlashCommand("list", "List all registered Minecraft servers")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

        await RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a server as the default for status commands.
    /// </summary>
    /// <param name="name">The name of the server to set as default.</param>
    [SlashCommand("default", "Set a server as the default")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task McDefault(string name)
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
    [SlashCommand("watch", "Set a channel to receive server status updates")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
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
    /// <param name="mode">The watch mode.</param>
    [SlashCommand("watchmode", "Set how status is displayed: embed, topic, or both")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task McWatchMode(string serverName,
        [Choice("Embed (edit in place)", 0)] [Choice("Channel Topic", 1)] [Choice("Both", 2)]
        int mode)
    {
        var server = await Service.SetWatchModeAsync(ctx.Guild.Id, serverName, (McWatchMode)mode);
        if (server == null)
        {
            await ErrorAsync(Strings.McServerNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ConfirmAsync(Strings.McWatchModeSet(ctx.Guild.Id, serverName, ((McWatchMode)mode).ToString()))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a custom embed template for a watched server's status messages.
    /// </summary>
    /// <param name="serverName">The name of the server.</param>
    /// <param name="template">The embed template JSON, or "-" to reset to default.</param>
    [SlashCommand("embed", "Set a custom embed template for server status")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task McEmbed(string serverName, string? template = null)
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
    [SlashCommand("player", "Look up a Minecraft player")]
    [CheckPermissions]
    public async Task McPlayer(string username)
    {
        await DeferAsync().ConfigureAwait(false);

        var profile = await Service.LookupPlayerAsync(username);
        if (profile == null)
        {
            await FollowupAsync(embed: new EmbedBuilder().WithErrorColor()
                .WithDescription(Strings.McPlayerNotFound(ctx.Guild.Id, username)).Build()).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(profile.Username)
            .WithThumbnailUrl(profile.AvatarUrl)
            .WithImageUrl(profile.SkinUrl)
            .AddField("UUID", $"`{profile.Uuid}`");

        await FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays a Minecraft player's skin render.
    /// </summary>
    /// <param name="username">The player's Minecraft username.</param>
    [SlashCommand("skin", "View a Minecraft player's skin")]
    [CheckPermissions]
    public async Task McSkin(string username)
    {
        await DeferAsync().ConfigureAwait(false);

        var profile = await Service.LookupPlayerAsync(username);
        if (profile == null)
        {
            await FollowupAsync(embed: new EmbedBuilder().WithErrorColor()
                .WithDescription(Strings.McPlayerNotFound(ctx.Guild.Id, username)).Build()).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.McSkinTitle(ctx.Guild.Id, profile.Username))
            .WithImageUrl(profile.SkinUrl);

        await FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
    }
}