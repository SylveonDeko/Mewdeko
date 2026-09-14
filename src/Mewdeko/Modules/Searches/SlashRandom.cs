using System.Collections.Immutable;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using MartineApiNet;
using MartineApiNet.Enums;
using MartineApiNet.Models.Images;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Mewdeko.Modules.Searches.Services;
using Newtonsoft.Json.Linq;
using Refit;

namespace Mewdeko.Modules.Searches;

/// <summary>
///     Slash commands for random images, memes, comics, facts and jokes.
/// </summary>
/// <param name="martineApi">The Martine API service.</param>
/// <param name="factory">The HTTP client factory.</param>
/// <param name="interactivity">The interactive service used for pagination.</param>
/// <param name="logger">The logger instance for structured logging.</param>
[Group("random", "Random images, memes, comics, facts and jokes")]
public class SlashRandom(
    MartineApi martineApi,
    IHttpClientFactory factory,
    InteractiveService interactivity,
    ILogger<SlashRandom> logger)
    : MewdekoSlashModuleBase<SearchesService>
{
    private const string XkcdUrl = "https://xkcd.com";

    private static readonly ImmutableDictionary<char, string> MemegenMap = new Dictionary<char, string>
    {
        {
            '?', "~q"
        },
        {
            '%', "~p"
        },
        {
            '#', "~h"
        },
        {
            '/', "~s"
        },
        {
            ' ', "-"
        },
        {
            '-', "--"
        },
        {
            '_', "__"
        },
        {
            '"', "''"
        }
    }.ToImmutableDictionary();

    /// <summary>
    ///     Displays a random cat image.
    /// </summary>
    [SlashCommand("cat", "Get a random cat image")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public Task RandomCat()
    {
        return InternalRandomImage(SearchesService.ImageTag.Cats);
    }

    /// <summary>
    ///     Displays a random dog image.
    /// </summary>
    [SlashCommand("dog", "Get a random dog image")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public Task RandomDog()
    {
        return InternalRandomImage(SearchesService.ImageTag.Dogs);
    }

    /// <summary>
    ///     Displays a random food image.
    /// </summary>
    [SlashCommand("food", "Get a random food image")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public Task RandomFood()
    {
        return InternalRandomImage(SearchesService.ImageTag.Food);
    }

    /// <summary>
    ///     Displays a random bird image.
    /// </summary>
    [SlashCommand("bird", "Get a random bird image")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public Task RandomBird()
    {
        return InternalRandomImage(SearchesService.ImageTag.Birds);
    }

    /// <summary>
    ///     Fetches and shares a random cat fact.
    /// </summary>
    [SlashCommand("catfact", "Get a random cat fact")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Catfact()
    {
        await DeferAsync().ConfigureAwait(false);
        using var http = factory.CreateClient();
        var response = await http.GetStringAsync("https://catfact.ninja/fact").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response))
        {
            await ErrorAsync(Strings.FetchFailed(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var fact = JObject.Parse(response)["fact"].ToString();
        await ctx.Interaction.SendConfirmAsync(Strings.Catfact(ctx.Guild.Id), fact).ConfigureAwait(false);
    }

    /// <summary>
    ///     Fetches and displays a random meme from Reddit.
    /// </summary>
    [SlashCommand("meme", "Get a random meme")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Meme()
    {
        await DeferAsync().ConfigureAwait(false);
        var image = await martineApi.RedditApi.GetRandomMeme(Toptype.year).ConfigureAwait(false);

        var button = new ComponentBuilder().WithButton("Another!", $"meme:{ctx.User.Id}");
        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = Strings.RedditAuthor(ctx.Guild.Id, image.Data.Author.Name)
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
        await ctx.Interaction.FollowupAsync(embed: em.Build(), components: button.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Fetches and displays a random post from a specified subreddit.
    /// </summary>
    /// <param name="subreddit">The subreddit from which to fetch a random post.</param>
    [SlashCommand("reddit", "Get a random post from a subreddit")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task RandomReddit([Summary("subreddit", "The subreddit name without r/")] string subreddit)
    {
        await DeferAsync().ConfigureAwait(false);

        if (Service.NsfwCheck(subreddit))
        {
            await ErrorAsync(Strings.SubredditIsNsfw(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var button = new ComponentBuilder().WithButton("Another!", $"randomreddit:{subreddit}.{ctx.User.Id}");
        RedditPost image;
        try
        {
            image = await martineApi.RedditApi.GetRandomFromSubreddit(subreddit).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            await ErrorAsync(Strings.SubredditNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            logger.LogError(
                "Seems that Meme fetching has failed. Here's the error:\nCode: {StatusCode}\nContent: {Content}",
                ex.StatusCode, ex.HasContent ? ex.Content : "No Content.");
            return;
        }

        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = Strings.RedditAuthor(ctx.Guild.Id, image.Data.Author.Name)
            },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder
            {
                Text = Strings.RedditUpvotesFooter(ctx.Guild.Id, image.Data.Upvotes, image.Data.Subreddit.Name)
            },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        await ctx.Interaction.FollowupAsync(embed: em.Build(), components: button.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays a RIP image with the user's name and avatar.
    /// </summary>
    /// <param name="usr">The user for whom to generate the RIP image.</param>
    [SlashCommand("rip", "Generate a rest in peace image for a user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Rip([Summary("user", "The user to rip")] IGuildUser usr)
    {
        await DeferAsync().ConfigureAwait(false);
        var av = usr.RealAvatarUrl();
        var picStream =
            await Service.GetRipPictureAsync(usr.Nickname ?? usr.Username, av).ConfigureAwait(false);
        await using var _ = picStream.ConfigureAwait(false);
        await ctx.Interaction.FollowupWithFileAsync(picStream, "rip.png",
            Strings.RipMessage(ctx.Guild.Id, Format.Bold(usr.ToString()), Format.Italics(ctx.User.ToString())));
    }

    /// <summary>
    ///     Fetches and displays an XKCD comic. When no number is given a random comic is shown.
    /// </summary>
    /// <param name="num">The number of the XKCD comic to fetch.</param>
    [SlashCommand("xkcd", "Show an xkcd comic")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Xkcd([Summary("number", "The comic number, random when omitted")] int? num = null)
    {
        num ??= new MewdekoRandom().Next(1, 2607);
        if (num < 1)
        {
            await ReplyErrorAsync(Strings.ComicNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        try
        {
            using var http = factory.CreateClient();
            var res = await http.GetStringAsync($"{XkcdUrl}/{num}/info.0.json").ConfigureAwait(false);

            var comic = JsonSerializer.Deserialize<XkcdComic>(res);
            var embed = new EmbedBuilder().WithColor(Mewdeko.OkColor)
                .WithImageUrl(comic.ImageLink)
                .WithAuthor(eab =>
                    eab.WithName(comic.Title).WithUrl($"{XkcdUrl}/{num}")
                        .WithIconUrl("https://xkcd.com/s/919f27.ico"))
                .AddField(efb =>
                    efb.WithName(Strings.ComicNumber(ctx.Guild.Id)).WithValue(comic.Num.ToString())
                        .WithIsInline(true))
                .AddField(efb =>
                    efb.WithName(Strings.Date(ctx.Guild.Id)).WithValue($"{comic.Month}/{comic.Year}")
                        .WithIsInline(true));
            var sent = await ctx.Interaction.FollowupAsync(embed: embed.Build())
                .ConfigureAwait(false);

            await Task.Delay(10000).ConfigureAwait(false);

            await sent.ModifyAsync(m =>
                    m.Embed = embed
                        .AddField(efb => efb.WithName("Alt").WithValue(comic.Alt).WithIsInline(false))
                        .Build())
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            await ReplyErrorAsync(Strings.ComicNotFound(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Generates a meme with the specified template and text.
    /// </summary>
    /// <param name="meme">The key of the meme template.</param>
    /// <param name="topText">The top line of text.</param>
    /// <param name="bottomText">The bottom line of text.</param>
    [SlashCommand("memegen", "Generate a meme from a template")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Memegen(
        [Summary("template", "The meme template")] [Autocomplete(typeof(MemegenTemplateAutocompleter))]
        string meme,
        [Summary("top", "The top text")] string? topText = null,
        [Summary("bottom", "The bottom text")] string? bottomText = null)
    {
        var memeUrl = $"https://api.memegen.link/{meme}";
        if (!string.IsNullOrWhiteSpace(topText) || !string.IsNullOrWhiteSpace(bottomText))
        {
            var top = string.IsNullOrWhiteSpace(topText) ? "_" : Replace(topText);
            memeUrl += $"/{top}";
            if (!string.IsNullOrWhiteSpace(bottomText))
                memeUrl += $"/{Replace(bottomText)}";
        }

        memeUrl += ".png";
        await ctx.Interaction.RespondAsync(memeUrl).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists available meme templates.
    /// </summary>
    [SlashCommand("meme-list", "List the available meme templates")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Memelist()
    {
        await DeferAsync().ConfigureAwait(false);
        using var http = factory.CreateClient("memelist");
        var res = await http.GetAsync("https://api.memegen.link/templates/")
            .ConfigureAwait(false);

        var rawJson = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

        var data = JsonSerializer.Deserialize<List<MemegenTemplateAutocompleter.MemegenTemplate>>(rawJson) ?? [];

        if (data.Count == 0)
        {
            await ErrorAsync(Strings.FetchFailed(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(data.Count / 15)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var templates = data.Skip(page * 15).Take(15).Aggregate("",
                (current, template) => current + $"**{template.Name}:**\n key: `{template.Id}`\n");
            return new PageBuilder()
                .WithOkColor()
                .WithDescription(templates);
        }
    }

    private async Task InternalRandomImage(SearchesService.ImageTag tag)
    {
        await DeferAsync().ConfigureAwait(false);
        try
        {
            var image = await Service.GetRandomImageAsync(tag).ConfigureAwait(false);
            var button = new ComponentBuilder().WithButton("Another!", $"randomimage:{tag}.{ctx.User.Id}");

            var em = new EmbedBuilder()
                .WithOkColor()
                .WithAuthor(Strings.RedditAuthor(ctx.Guild.Id, image.Data.Author.Name))
                .WithDescription($"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})")
                .WithFooter(Strings.RedditUpvotesFooter(ctx.Guild.Id, image.Data.Upvotes, image.Data.Subreddit.Name))
                .WithImageUrl(image.Data.ImageUrl);

            await ctx.Interaction.FollowupAsync(embed: em.Build(), components: button.Build())
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            await ErrorAsync(Strings.FetchFailed(ctx.Guild.Id)).ConfigureAwait(false);

            logger.LogError(
                "Image fetch failed. Error:\nCode: {StatusCode}\nContent: {Content}",
                ex.StatusCode,
                ex.HasContent ? ex.Content : "No Content"
            );
        }
    }

    private static string Replace(string input)
    {
        var sb = new StringBuilder();

        foreach (var c in input)
        {
            if (MemegenMap.TryGetValue(c, out var tmp))
                sb.Append(tmp);
            else
                sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Slash commands for retrieving various types of jokes.
    /// </summary>
    [Group("joke", "Random jokes")]
    public class SlashRandomJoke : MewdekoSlashSubmodule<SearchesService>
    {
        /// <summary>
        ///     Retrieves a Yo Mama joke.
        /// </summary>
        [SlashCommand("yomama", "Get a yo mama joke")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Yomama()
        {
            await DeferAsync().ConfigureAwait(false);
            await ConfirmAsync(await Service.GetYomamaJoke().ConfigureAwait(false)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves a random joke.
        /// </summary>
        [SlashCommand("random", "Get a random joke")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Randjoke()
        {
            await DeferAsync().ConfigureAwait(false);
            var (setup, punchline) = await Service.GetRandomJoke().ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(setup, punchline).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves a Chuck Norris joke.
        /// </summary>
        [SlashCommand("chuck", "Get a Chuck Norris joke")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task ChuckNorris()
        {
            await DeferAsync().ConfigureAwait(false);
            await ConfirmAsync(await Service.GetChuckNorrisJoke().ConfigureAwait(false)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves a joke related to World of Warcraft.
        /// </summary>
        [SlashCommand("wow", "Get a World of Warcraft joke")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task WowJoke()
        {
            if (Service.WowJokes.Count == 0)
            {
                await ReplyErrorAsync(Strings.JokesNotLoaded(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var joke = Service.WowJokes[new MewdekoRandom().Next(0, Service.WowJokes.Count)];
            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                .WithTitle(joke.Question).WithDescription(joke.Answer).Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves a random magic item description related to World of Warcraft.
        /// </summary>
        [SlashCommand("magic-item", "Get a random World of Warcraft magic item")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task MagicItem()
        {
            if (Service.MagicItems.Count == 0)
            {
                await ReplyErrorAsync(Strings.MagicitemsNotLoaded(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var item = Service.MagicItems[new MewdekoRandom().Next(0, Service.MagicItems.Count)];

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                    .WithTitle(Strings.JokeSparkle(ctx.Guild.Id, item.Name)).WithDescription(item.Description)
                    .Build())
                .ConfigureAwait(false);
        }
    }
}