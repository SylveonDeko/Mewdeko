using System.Net.Http;
using System.Text.RegularExpressions;
using Discord.Interactions;
using Discord.Net;
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
            var emoteName = i.Name; // Default to the emote name

            // Define a pattern to find the emote in the message
            var pattern = $"<:{i.Name}:[0-9]+>";
            var match = Regex.Match(message.Content, pattern);

            if (match.Success)
            {
                // Find the index immediately after the emote match
                var indexAfterEmote = match.Index + match.Length;

                // Get the substring from the message that comes after the emote
                var potentialNamePart = message.Content.Substring(indexAfterEmote).Trim();

                // Split the remaining message by spaces and take the first word if any
                var parts = potentialNamePart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // Use the provided name only if there is exactly one emote and one potential name
                if (parts.Length > 0 && tags.Count() == 1)
                {
                    emoteName = parts[0]; // Custom name provided by the user
                }
            }

            using var http = httpFactory.CreateClient();
            using var sr = await http.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            {
                try
                {
                    var emote = await ctx.Guild.CreateEmoteAsync(emoteName, new Image(imgStream)).ConfigureAwait(false);
                    emotes.Add($"{emote} {Format.Code(emote.Name)}");
                }
                catch (HttpException httpEx) when (httpEx.HttpCode == System.Net.HttpStatusCode.BadRequest)
                {
                    if (httpEx.DiscordCode.HasValue && httpEx.DiscordCode.Value == (DiscordErrorCode)30008)
                    {
                        // check if the error is 30008
                        errored.Add($"Unable to add '{i.Name}'. Discord server reports no free emoji slots.");
                    }
                    else
                    {
                        // other HttpExceptions
                        Log.Information($"Failed to add emotes. Message: {httpEx.Message}");
                        errored.Add($"{i.Name}\n{i.Url}");
                    }
                }
                catch (Exception ex)
                {
                    // handle non-HTTP exceptions
                    Log.Information($"Failed to add emotes. Message: {ex.Message}");
                    errored.Add($"{emoteName}\n{i.Url}");
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