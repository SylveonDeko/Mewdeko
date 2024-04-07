using System.Net.Http;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Services.Settings;
using Serilog;
using Image = Discord.Image;

namespace Mewdeko.Modules.Server_Management;

/// <summary>
/// A module for stealing emotes and stickers from messages and adding them to the server.
/// </summary>
public class EmoteStealer(IHttpClientFactory httpFactory, BotConfigService config) : MewdekoSlashCommandModule
{
    /// <summary>
    /// Steals emotes from a message and adds them to the server's emote collection.
    /// </summary>
    /// <param name="message">The message containing emotes to be stolen.</param>
    /// <remarks>
    /// This command requires the "Manage Emojis and Stickers" permission.
    /// It goes through all the emotes in the specified message, downloads them, and attempts to add them to the guild.
    /// Errors are logged, and a summary of successful and failed additions is provided.
    /// </remarks>
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

    /// <summary>
    /// Steals stickers from a message and adds them to the server's sticker collection.
    /// </summary>
    /// <param name="message">The message containing stickers to be stolen.</param>
    /// <remarks>
    /// Similar to the emote stealing function, this command requires "Manage Emojis and Stickers" permission.
    /// It processes all the stickers in the provided message, attempting to add each to the server.
    /// Successes and failures are reported, with errors logged for troubleshooting.
    /// </remarks>
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
        await ctx.Interaction.FollowupAsync(embed: eb.Build(), ephemeral: true).ConfigureAwait(false);
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