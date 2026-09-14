using System.IO;
using System.Net.Http;
using Discord.Interactions;
using Discord.Net;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Services.Settings;
using Image = Discord.Image;

namespace Mewdeko.Modules.Server_Management;

/// <summary>
///     Slash commands for server name, images, emotes and permission viewing.
/// </summary>
/// <param name="factory">The http client factory used to download images.</param>
/// <param name="config">The bot configuration settings.</param>
[Group("serverconfig", "Server name, icon, banner, splash and emotes")]
public class SlashServerConfig(IHttpClientFactory factory, BotConfigService config) : MewdekoSlashCommandModule
{
    private async Task<string?> ResolveImageUrl(IAttachment? image, string? url)
    {
        if (image is not null) return image.Url;
        if (!string.IsNullOrWhiteSpace(url)) return url;
        await ErrorAsync(Strings.NoImageProvided(ctx.Guild.Id)).ConfigureAwait(false);
        return null;
    }

    private async Task<Stream> DownloadImage(string url)
    {
        var uri = new Uri(url);
        using var http = factory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        return imgData.ToStream();
    }

    private async Task<GuildEmote?> ResolveGuildEmote(string emote)
    {
        GuildEmote? found = null;
        if (Emote.TryParse(emote, out var parsed))
        {
            try
            {
                found = await ctx.Guild.GetEmoteAsync(parsed.Id).ConfigureAwait(false);
            }
            catch (HttpException)
            {
            }
        }
        else
        {
            var name = emote.Trim(':');
            found = ctx.Guild.Emotes.FirstOrDefault(x =>
                x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        if (found is not null) return found;
        await ErrorAsync(Strings.EmoteNotFromGuild(ctx.Guild.Id)).ConfigureAwait(false);
        return null;
    }

    private static string BuildPermissionList(GuildPermissions perms)
    {
        var allowed = Enum.GetValues<GuildPermission>()
            .Where(perms.Has)
            .Select(i => $"**{i}**");
        return string.Join("\n", allowed);
    }

    /// <summary>
    ///     Displays the list of allowed guild permissions for you, a user, or a role.
    /// </summary>
    /// <param name="user">Optional user whose permissions will be displayed.</param>
    /// <param name="role">Optional role whose permissions will be displayed.</param>
    [SlashCommand("perm-view", "Shows the guild permissions of you, a user or a role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task PermView([Summary("user", "The user to inspect")] IGuildUser? user = null,
        [Summary("role", "The role to inspect")]
        IRole? role = null)
    {
        var eb = new EmbedBuilder().WithOkColor();
        if (role is not null)
        {
            eb.WithTitle(Strings.ListAllowedPermsFor(ctx.Guild.Id, Strings.ListAllowedPerms(ctx.Guild.Id), role));
            eb.WithDescription(BuildPermissionList(role.Permissions));
        }
        else if (user is not null)
        {
            eb.WithTitle(Strings.ListAllowedPermsFor(ctx.Guild.Id, Strings.ListAllowedPerms(ctx.Guild.Id), user));
            eb.WithDescription(BuildPermissionList(user.GuildPermissions));
        }
        else
        {
            eb.WithTitle(Strings.ListAllowedPerms(ctx.Guild.Id));
            eb.WithDescription(BuildPermissionList(((IGuildUser)ctx.User).GuildPermissions));
        }

        await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the name of the server.
    /// </summary>
    /// <param name="name">The new name for the server.</param>
    [SlashCommand("name", "Sets the server name")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task SetServerName([Summary("name", "The new server name")] string name)
    {
        await ctx.Guild.ModifyAsync(x => x.Name = name).ConfigureAwait(false);
        await ConfirmAsync(Strings.ServerNameSet(ctx.Guild.Id, name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the icon of the server from an attachment or url.
    /// </summary>
    /// <param name="image">The image attachment to use as the icon.</param>
    /// <param name="url">The url of the new server icon.</param>
    [SlashCommand("icon", "Sets the server icon")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task SetIcon([Summary("image", "The image to use")] IAttachment? image = null,
        [Summary("url", "A direct image url to use")]
        string? url = null)
    {
        var acturl = await ResolveImageUrl(image, url).ConfigureAwait(false);
        if (acturl is null) return;
        await DeferAsync().ConfigureAwait(false);
        var imgStream = await DownloadImage(acturl).ConfigureAwait(false);
        await using var _ = imgStream.ConfigureAwait(false);
        await ctx.Guild.ModifyAsync(x => x.Icon = new Image(imgStream)).ConfigureAwait(false);
        await ConfirmAsync(Strings.ServerIconSet(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the banner of the server from an attachment or url.
    /// </summary>
    /// <param name="image">The image attachment to use as the banner.</param>
    /// <param name="url">The url of the new server banner.</param>
    [SlashCommand("banner", "Sets the server banner")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task SetBanner([Summary("image", "The image to use")] IAttachment? image = null,
        [Summary("url", "A direct image url to use")]
        string? url = null)
    {
        var acturl = await ResolveImageUrl(image, url).ConfigureAwait(false);
        if (acturl is null) return;
        await DeferAsync().ConfigureAwait(false);
        var imgStream = await DownloadImage(acturl).ConfigureAwait(false);
        await using var _ = imgStream.ConfigureAwait(false);
        await ctx.Guild.ModifyAsync(x => x.Banner = new Image(imgStream)).ConfigureAwait(false);
        await ConfirmAsync(Strings.ServerBannerSet(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the invite splash image of the server from an attachment or url.
    /// </summary>
    /// <param name="image">The image attachment to use as the splash.</param>
    /// <param name="url">The url of the new splash image.</param>
    [SlashCommand("splash", "Sets the server invite splash image")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task SetSplash([Summary("image", "The image to use")] IAttachment? image = null,
        [Summary("url", "A direct image url to use")]
        string? url = null)
    {
        var acturl = await ResolveImageUrl(image, url).ConfigureAwait(false);
        if (acturl is null) return;
        await DeferAsync().ConfigureAwait(false);
        var imgStream = await DownloadImage(acturl).ConfigureAwait(false);
        await using var _ = imgStream.ConfigureAwait(false);
        await ctx.Guild.ModifyAsync(x => x.Splash = new Image(imgStream)).ConfigureAwait(false);
        await ConfirmAsync(Strings.SplashImageSet(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a new emote to the server from an attachment, an image url, or an existing emote.
    /// </summary>
    /// <param name="name">The name of the emote.</param>
    /// <param name="image">The image attachment to use.</param>
    /// <param name="url">The url of the emote image, or an existing emote to copy.</param>
    [SlashCommand("add-emote", "Adds an emote from an image or another emote")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageEmojisAndStickers)]
    [RequireBotPermission(GuildPermission.ManageEmojisAndStickers)]
    public async Task AddEmote([Summary("name", "The emote name")] string name,
        [Summary("image", "The image to use")] IAttachment? image = null,
        [Summary("url", "A direct image url or an emote to copy")]
        string? url = null)
    {
        if (image is null && !string.IsNullOrWhiteSpace(url) && Emote.TryParse(url, out var existing))
            url = existing.Url;

        var acturl = await ResolveImageUrl(image, url).ConfigureAwait(false);
        if (acturl is null) return;
        await DeferAsync().ConfigureAwait(false);
        var imgStream = await DownloadImage(acturl).ConfigureAwait(false);
        await using var _ = imgStream.ConfigureAwait(false);
        try
        {
            var emote = await ctx.Guild.CreateEmoteAsync(name, new Image(imgStream)).ConfigureAwait(false);
            await ConfirmAsync(Strings.EmoteCreated(ctx.Guild.Id, emote, Format.Code(name))).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ErrorAsync(Strings.EmoteAddFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes an emote from the server.
    /// </summary>
    /// <param name="emote">The emote to remove, either the emote itself or its name.</param>
    [SlashCommand("remove-emote", "Removes an emote from the server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageEmojisAndStickers)]
    [RequireBotPermission(GuildPermission.ManageEmojisAndStickers)]
    public async Task RemoveEmote([Summary("emote", "The emote or its name")] string emote)
    {
        var emote1 = await ResolveGuildEmote(emote).ConfigureAwait(false);
        if (emote1 is null) return;
        try
        {
            await ctx.Guild.DeleteEmoteAsync(emote1).ConfigureAwait(false);
            await ConfirmAsync(Strings.EmoteDeleted(ctx.Guild.Id, emote1)).ConfigureAwait(false);
        }
        catch (HttpException)
        {
            await ErrorAsync(Strings.EmoteNotFromGuild(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Renames an existing emote on the server.
    /// </summary>
    /// <param name="emote">The existing emote to rename, either the emote itself or its name.</param>
    /// <param name="name">The new name for the emote.</param>
    [SlashCommand("rename-emote", "Renames a server emote")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageEmojisAndStickers)]
    [RequireBotPermission(GuildPermission.ManageEmojisAndStickers)]
    public async Task RenameEmote([Summary("emote", "The emote or its name")] string emote,
        [Summary("name", "The new name")] string name)
    {
        if (name.StartsWith('<'))
        {
            await ErrorAsync(Strings.EmoteInvalidName(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var emote1 = await ResolveGuildEmote(emote).ConfigureAwait(false);
        if (emote1 is null) return;
        try
        {
            var ogname = emote1.Name;
            await ctx.Guild.ModifyEmoteAsync(emote1, x => x.Name = name).ConfigureAwait(false);
            var emote2 = await ctx.Guild.GetEmoteAsync(emote1.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.EmoteRenamed(ctx.Guild.Id, emote1, Format.Code(ogname),
                Format.Code(emote2.Name))).ConfigureAwait(false);
        }
        catch (HttpException)
        {
            await ErrorAsync(Strings.EmoteWrongGuild(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Steals the given emotes and adds them to the server locked to a specified role.
    /// </summary>
    /// <param name="role">The role to lock the emotes to.</param>
    /// <param name="emotes">The emotes to steal, separated by spaces.</param>
    [SlashCommand("steal-for-role", "Adds the given emotes to the server, usable only by a role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageEmojisAndStickers)]
    [RequireBotPermission(GuildPermission.ManageEmojisAndStickers)]
    public async Task StealForRole([Summary("role", "The role that can use the emotes")] IRole role,
        [Summary("emotes", "Emotes to add, separated by spaces")]
        string emotes)
    {
        var tags = emotes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => Emote.TryParse(x, out var parsed) ? parsed : null)
            .Where(x => x is not null)
            .DistinctBy(x => x.Id)
            .ToList();
        if (tags.Count == 0)
        {
            await ErrorAsync(Strings.NoEmotesProvided(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var eb = new EmbedBuilder
        {
            Description = Strings.AddingEmotesToRole(ctx.Guild.Id, config.Data.LoadingEmote, role.Mention),
            Color = Mewdeko.OkColor
        };
        var list = new Optional<IEnumerable<IRole>>([
            role
        ]);
        var errored = new List<string>();
        var added = new List<string>();
        var msg = await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);

        foreach (var i in tags)
        {
            using var http = factory.CreateClient();
            using var sr = await http.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            try
            {
                var emote = await ctx.Guild.CreateEmoteAsync(i.Name, new Image(imgStream), list)
                    .ConfigureAwait(false);
                added.Add($"{emote} {Format.Code(emote.Name)}");
            }
            catch (Exception)
            {
                errored.Add($"{i.Name}\n{i.Url}");
            }
        }

        var b = new EmbedBuilder
        {
            Color = Mewdeko.OkColor
        };
        if (added.Count > 0)
            b.WithDescription(Strings.AddedEmotesToRole(ctx.Guild.Id, added.Count, role.Mention,
                string.Join("\n", added)));
        if (errored.Count > 0)
            b.AddField(Strings.ErroredEmotes(ctx.Guild.Id, errored.Count), string.Join("\n\n", errored));
        await msg.ModifyAsync(x => x.Embed = b.Build()).ConfigureAwait(false);
    }
}