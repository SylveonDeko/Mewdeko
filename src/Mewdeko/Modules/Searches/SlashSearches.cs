using Discord.Interactions;
using MartineApiNet;
using MartineApiNet.Enums;
using Mewdeko.Modules.Searches.Services;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Searches;

public class SlashSearches : MewdekoSlashModuleBase<SearchesService>
{
    private readonly MartineApi _martineApi;
    public SlashSearches(MartineApi martineApi) => _martineApi = martineApi;

    [ComponentInteraction("meme:*")]
    public async Task Meme(string userid)
    {
        await DeferAsync();
        ulong.TryParse(userid, out var id);
        var image = await _martineApi.RedditApi.GetRandomMeme(Toptype.year);
        while (SearchesService.CheckIfAlreadyPosted(ctx.Guild, image.Data.ImageUrl))
        {
            image = await _martineApi.RedditApi.GetRandomMeme();
            await Task.Delay(500);
        }
        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder { Name = $"u/{image.Data.Author.Name}" },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder { Text = $"{image.Data.Upvotes} Upvotes {image.Data.Downvotes} Downvotes | r/{image.Data.Subreddit.Name} | Powered by MartineApi" },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        if (ctx.User.Id != id)
        {
            await ctx.Interaction.FollowupAsync(embed: em.Build(), ephemeral: true);
            return;
        }
        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = em.Build());
    }

    [ComponentInteraction("randomreddit:*.*")]
    public async Task RandomReddit(string subreddit, string userId)
    {
        await DeferAsync();
        ulong.TryParse(userId, out var id);

        var image = await _martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.year);
        while (SearchesService.CheckIfAlreadyPosted(ctx.Guild, image.Data.ImageUrl))
        {
            image = await _martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.year);
            await Task.Delay(500);
        }

        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder { Name = $"u/{image.Data.Author.Name}" },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder { Text = $"{image.Data.Upvotes} Upvotes! | r/{image.Data.Subreddit.Name} Powered by martineAPI" },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        if (ctx.User.Id != id)
        {
            await ctx.Interaction.FollowupAsync(embed: em.Build(), ephemeral: true);
            return;
        }
        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = em.Build());
    }
}