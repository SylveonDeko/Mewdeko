using System.Threading.Tasks;
using Discord.Interactions;
using MartineApiNet;
using MartineApiNet.Enums;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

public class SlashSearches : MewdekoSlashModuleBase<SearchesService>
{
    private readonly MartineApi martineApi;
    public SlashSearches(MartineApi martineApi) => this.martineApi = martineApi;

    [ComponentInteraction("meme:*", true)]
    public async Task Meme(string userid)
    {
        await DeferAsync().ConfigureAwait(false);
        ulong.TryParse(userid, out var id);
        var image = await martineApi.RedditApi.GetRandomMeme(Toptype.year).ConfigureAwait(false);
        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = $"u/{image.Data.Author.Name}"
            },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder
            {
                Text = $"{image.Data.Upvotes} Upvotes {image.Data.Downvotes} Downvotes | r/{image.Data.Subreddit.Name} | Powered by MartineApi"
            },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        if (ctx.User.Id != id)
        {
            await ctx.Interaction.FollowupAsync(embed: em.Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = em.Build()).ConfigureAwait(false);
    }

    [ComponentInteraction("randomreddit:*.*", true)]
    public async Task RandomReddit(string subreddit, string userId)
    {
        await DeferAsync().ConfigureAwait(false);
        ulong.TryParse(userId, out var id);

        var image = await martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.year).ConfigureAwait(false);

        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = $"u/{image.Data.Author.Name}"
            },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder
            {
                Text = $"{image.Data.Upvotes} Upvotes! | r/{image.Data.Subreddit.Name} Powered by martineAPI"
            },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        if (ctx.User.Id != id)
        {
            await ctx.Interaction.FollowupAsync(embed: em.Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = em.Build()).ConfigureAwait(false);
    }
}