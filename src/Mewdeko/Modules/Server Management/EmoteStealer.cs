using System.Net.Http;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Services.Settings;
using Serilog;
using Image = Discord.Image;

namespace Mewdeko.Modules.Server_Management;

public class EmoteStealer : MewdekoSlashCommandModule
{
    private readonly IHttpClientFactory httpFactory;
    private readonly BotConfigService config;

    public EmoteStealer(IHttpClientFactory factory, BotConfigService config)
    {
        httpFactory = factory;
        this.config = config;
    }

    [MessageCommand("Steal Emotes"),
     RequireBotPermission(GuildPermission.ManageEmojisAndStickers),
     SlashUserPerm(GuildPermission.ManageEmojisAndStickers),
     CheckPermissions]
    public async Task Steal(IMessage message)
    {
        await ctx.Interaction.DeferAsync(true).ConfigureAwait(false);
        await ctx.Interaction.FollowupAsync(
            "If the message below loads infinitely, discord has limited the servers emoji upload limit. And no, this cant be circumvented with other bots (to my knowledge).");
        var eb = new EmbedBuilder
        {
            Description = $"{config.Data.LoadingEmote} Adding Emotes...", Color = Mewdeko.OkColor
        };
        var tags = message.Tags.Where(x => x.Type == TagType.Emoji).Select(x => (Emote)x.Value).Distinct();
        if (!tags.Any())
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync("No emotes in this message!").ConfigureAwait(false);
            return;
        }

        var errored = new List<string>();
        var emotes = new List<string>();
        var msg = await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
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

    [MessageCommand("Steal Sticker"),
     RequireBotPermission(GuildPermission.ManageEmojisAndStickers),
     SlashUserPerm(GuildPermission.ManageEmojisAndStickers),
     CheckPermissions]
    public async Task StealSticker(IMessage message)
    {
        await ctx.Interaction.DeferAsync(true).ConfigureAwait(false);
        await ctx.Interaction.FollowupAsync(
            "If the message below loads infinitely, discord has limited the servers stickers upload limit. And no, this cant be circumvented with other bots (to my knowledge).");
        var eb = new EmbedBuilder
        {
            Description = $"{config.Data.LoadingEmote} Adding stickers...", Color = Mewdeko.OkColor
        };
        var tags = message.Stickers.Select(x => x as SocketUnknownSticker).Distinct();
        if (!tags.Any())
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync("No stickers in this message!").ConfigureAwait(false);
            return;
        }

        var errored = new List<string>();
        var emotes = new List<string>();
        var msg = await ctx.Interaction.FollowupAsync(embed: eb.Build(), ephemeral: true).ConfigureAwait(false);
        foreach (var i in tags)
        {
            using var http = httpFactory.CreateClient();
            using var sr = await http.GetAsync(i.GetStickerUrl(), HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            {
                try
                {
                    var emote = await ctx.Guild.CreateStickerAsync(i.Name, new Image(imgStream), new[]
                        {
                            "Mewdeko"
                        }, i.Description)
                        .ConfigureAwait(false);
                    emotes.Add($"{emote.Name} [Url]({(emote.GetStickerUrl())})");
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                    errored.Add($"{i.Name} | [Url]({i.GetStickerUrl()})");
                }
            }
        }

        var b = new EmbedBuilder
        {
            Color = Mewdeko.OkColor
        };
        if (emotes.Count > 0) b.WithDescription($"**Added Stickers**\n{string.Join("\n", emotes)}");
        if (errored.Count > 0) b.AddField("Errored Stickers", string.Join("\n\n", errored));
        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = b.Build()).ConfigureAwait(false);
    }
}