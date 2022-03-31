using Discord;
using Discord.Interactions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using System.Net.Http;
using Image = Discord.Image;

namespace Mewdeko.Modules.Server_Management;

public class EmoteStealer : MewdekoSlashCommandModule
{
    private readonly IHttpClientFactory _httpFactory;
    
    public EmoteStealer(IHttpClientFactory factory) => _httpFactory = factory;
    
    [MessageCommand("Steal Emotes"), 
     RequireBotPermission(GuildPermission.ManageEmojisAndStickers), 
     SlashUserPerm(GuildPermission.ManageEmojisAndStickers),
    CheckPermissions]
    public async Task Steal(IMessage message)
    {
        await ctx.Interaction.DeferAsync(true);
        var eb = new EmbedBuilder
        {
            Description = "<a:loading:900381735244689469> Adding Emotes...",
            Color = Mewdeko.OkColor
        };
        var tags = message.Tags.Where(x => x.Type == TagType.Emoji).Select(x => (Emote)x.Value);
        if (!tags.Any())
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync("No emotes in this message!");
            return;
        }
        var errored = new List<string>();
        var emotes = new List<string>();
        await ctx.Interaction.FollowupAsync(embed: eb.Build());
        foreach (var i in tags)
        {
            using var http = _httpFactory.CreateClient();
            using var sr = await http.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            await using var imgStream = imgData.ToStream();
            {
                try
                {
                    var emote = await ctx.Guild.CreateEmoteAsync(i.Name, new Image(imgStream));
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
        if (emotes.Any()) b.WithDescription($"**Added Emotes**\n{string.Join("\n", emotes)}");
        if (errored.Any()) b.AddField("Errored Emotes", string.Join("\n\n", errored));
        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = b.Build());
    }
}
