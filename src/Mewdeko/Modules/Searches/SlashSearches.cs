using Discord.Interactions;
using MartineApiNet;
using MartineApiNet.Enums;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

/// <summary>
/// Provides slash command interactions for searching and retrieving content from various sources.
/// </summary>
public class SlashSearches(MartineApi martineApi) : MewdekoSlashModuleBase<SearchesService>
{
    /// <summary>
    /// Handles the "meme" component interaction, fetching and showing a random meme.
    /// </summary>
    /// <param name="userid">The Discord user ID who initiated the meme fetch interaction.</param>
    /// <remarks>
    /// This interaction command fetches a random meme from the configured sources via the Martine API
    /// and presents it to the user who triggered the interaction.
    /// The command supports ephemerality, showing the response only to the initiating user.
    /// </remarks>
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
                Text =
                    $"{image.Data.Upvotes} Upvotes {image.Data.Downvotes} Downvotes | r/{image.Data.Subreddit.Name} | Powered by MartineApi"
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

    /// <summary>
    /// Handles the "randomreddit" component interaction, fetching and displaying a random post from a specified subreddit.
    /// </summary>
    /// <param name="subreddit">The subreddit from which to fetch a random post.</param>
    /// <param name="userId">The Discord user ID who initiated the subreddit fetch interaction.</param>
    /// <remarks>
    /// This interaction command fetches a random post from the specified subreddit via the Martine API.
    /// It supports ephemerality, allowing the response to be visible only to the user who initiated the interaction.
    /// </remarks>
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