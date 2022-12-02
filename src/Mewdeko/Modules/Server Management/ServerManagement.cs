using System.Net.Http;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.Net;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Server_Management.Services;
using Mewdeko.Services.Settings;
using Image = Discord.Image;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement : MewdekoModuleBase<ServerManagementService>
{
    private readonly IHttpClientFactory httpFactory;
    private readonly BotConfigService config;

    public ServerManagement(IHttpClientFactory factory, BotConfigService config)
    {
        httpFactory = factory;
        this.config = config;
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task PermView()
    {
        var perms = ((IGuildUser)ctx.User).GuildPermissions;
        var eb = new EmbedBuilder();
        eb.WithTitle("List of allowed perms");
        eb.WithOkColor();
        var allowed = perms.ToList().Select(i => $"**{i}**").ToList();

        eb.WithDescription(string.Join("\n", allowed));
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(0)]
    public async Task PermView(IGuildUser user)
    {
        var perms = user.GuildPermissions;
        var eb = new EmbedBuilder();
        eb.WithTitle($"List of allowed perms for {user}");
        eb.WithOkColor();
        var allowed = perms.ToList().Select(i => $"**{i}**").ToList();

        eb.WithDescription(string.Join("\n", allowed));
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task PermView(IRole user)
    {
        var perms = user.Permissions;
        var eb = new EmbedBuilder();
        eb.WithTitle($"List of allowed perms for {user}");
        eb.WithOkColor();
        var allowed = perms.ToList().Select(i => $"**{i}**").ToList();

        eb.WithDescription(string.Join("\n", allowed));
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task SetSplash(string img)
    {
        var guild = ctx.Guild;
        var uri = new Uri(img);
        using var http = httpFactory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await guild.ModifyAsync(x => x.Splash = new Image(imgStream)).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync("New splash image has been set!").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task SetIcon(string img)
    {
        var guild = ctx.Guild;
        var uri = new Uri(img);
        using var http = httpFactory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await guild.ModifyAsync(x => x.Icon = new Image(imgStream)).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync("New server icon has been set!").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task SetBanner(string img)
    {
        var guild = ctx.Guild;
        var uri = new Uri(img);
        using var http = httpFactory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await guild.ModifyAsync(x => x.Banner = new Image(imgStream)).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync("New server banner has been set!").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task SetServerName([Remainder] string name)
    {
        var guild = ctx.Guild;
        await guild.ModifyAsync(x => x.Name = name).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"Succesfuly set server name to {name}").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageEmojisAndStickers), BotPerm(GuildPermission.ManageEmojisAndStickers), Priority(0)]
    public async Task AddEmote(string name, string? url = null)
    {
        string acturl;
        if (string.IsNullOrWhiteSpace(url))
        {
            var tags = ctx.Message.Attachments.FirstOrDefault();
            acturl = tags.Url;
        }
        else if (url.StartsWith("<"))
        {
            var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value);
            var result = tags.Select(m => m.Url);
            acturl = string.Join("", result);
        }
        else
        {
            acturl = url;
        }

        var uri = new Uri(acturl);
        using var http = httpFactory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        try
        {
            var emote = await ctx.Guild.CreateEmoteAsync(name, new Image(imgStream)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"{emote} with the name {Format.Code(name)} created!").ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ctx.Channel.SendErrorAsync(
                "The emote could not be added because it is either: Too Big(Over 256kb), != a direct link, Or exceeds server emoji limit.").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageEmojisAndStickers),
     BotPerm(GuildPermission.ManageEmojisAndStickers), RequireContext(ContextType.Guild)]
    public async Task RemoveEmote(string _)
    {
        var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value)
            .FirstOrDefault();
        try
        {
            var emote1 = await ctx.Guild.GetEmoteAsync(tags.Id).ConfigureAwait(false);
            await ctx.Guild.DeleteEmoteAsync(emote1).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"{emote1} has been deleted!").ConfigureAwait(false);
        }
        catch (HttpException)
        {
            await ctx.Channel.SendErrorAsync("This emote is not from this guild!").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageEmojisAndStickers),
     BotPerm(GuildPermission.ManageEmojisAndStickers), RequireContext(ContextType.Guild)]
    public async Task RenameEmote(string emote, string name)
    {
        if (name.StartsWith("<"))
        {
            await ctx.Channel.SendErrorAsync("You cant use an emote as a name!").ConfigureAwait(false);
            return;
        }

        var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value)
            .FirstOrDefault();
        try
        {
            var emote1 = await ctx.Guild.GetEmoteAsync(tags.Id).ConfigureAwait(false);
            var ogname = emote1.Name;
            await ctx.Guild.ModifyEmoteAsync(emote1, x => x.Name = name).ConfigureAwait(false);
            var emote2 = await ctx.Guild.GetEmoteAsync(tags.Id).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"{emote1} has been renamed from {Format.Code(ogname)} to {Format.Code(emote2.Name)}").ConfigureAwait(false);
        }
        catch (HttpException)
        {
            await ctx.Channel.SendErrorAsync("This emote != from this guild!").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageEmojisAndStickers), BotPerm(GuildPermission.ManageEmojisAndStickers), Priority(1)]
    public async Task StealEmotes([Remainder] string e)
    {
        var eb = new EmbedBuilder
        {
            Description = $"{config.Data.LoadingEmote} Adding Emotes...", Color = Mewdeko.OkColor
        };
        var errored = new List<string>();
        var emotes = new List<string>();
        var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value).Distinct();
        if (!tags.Any()) return;
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        foreach (var i in tags)
        {
            using var http = httpFactory.CreateClient();
            using var sr = await http.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            {
                try
                {
                    var emote = await ctx.Guild.CreateEmoteAsync(i.Name, new Image(imgStream)).ConfigureAwait(false);
                    emotes.Add($"{emote} {Format.Code(emote.Name)}");
                }
                catch (Exception)
                {
                    errored.Add($"{i.Name}\n{i.Url}");
                }
            }
        }

        var b = new EmbedBuilder
        {
            Color = Mewdeko.OkColor
        };
        if (emotes.Count > 0) b.WithDescription($"**Added Emotes**\n{string.Join("\n", emotes)}");
        if (errored.Count > 0) b.AddField("Errored Emotes", string.Join("\n\n", errored));
        await msg.ModifyAsync(x => x.Embed = b.Build()).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageEmojisAndStickers), BotPerm(GuildPermission.ManageEmojisAndStickers), Priority(0)]
    public async Task StealForRole(IRole role, [Remainder] string e)
    {
        var eb = new EmbedBuilder
        {
            Description = $"{config.Data.LoadingEmote} Adding Emotes to {role.Mention}...", Color = Mewdeko.OkColor
        };
        var list = new Optional<IEnumerable<IRole>>(new[]
        {
            role
        });
        var errored = new List<string>();
        var emotes = new List<string>();
        var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value).Distinct();
        if (!tags.Any()) return;
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);

        foreach (var i in tags)
        {
            using var http = httpFactory.CreateClient();
            using var sr = await http.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            {
                try
                {
                    var emote = await ctx.Guild.CreateEmoteAsync(i.Name, new Image(imgStream), list).ConfigureAwait(false);
                    emotes.Add($"{emote} {Format.Code(emote.Name)}");
                }
                catch (Exception)
                {
                    errored.Add($"{i.Name}\n{i.Url}");
                }
            }
        }

        var b = new EmbedBuilder
        {
            Color = Mewdeko.OkColor
        };
        if (emotes.Count > 0)
            b.WithDescription($"**Added {emotes.Count} Emotes to {role.Mention}**\n{string.Join("\n", emotes)}");
        if (errored.Count > 0) b.AddField($"{errored.Count} Errored Emotes", string.Join("\n\n", errored));
        await msg.ModifyAsync(x => x.Embed = b.Build()).ConfigureAwait(false);
    }
}