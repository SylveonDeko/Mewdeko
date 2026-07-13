using System.Net.Http;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Nsfw.Common;

namespace Mewdeko.Modules.Nsfw;

/// <summary>
///     Represents a response containing information about a URL.
/// </summary>
public record UrlReply
{
    /// <summary>
    ///     Gets or initializes the error message, if any.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    ///     Gets or initializes the URL.
    /// </summary>
    public string Url { get; init; }

    /// <summary>
    ///     Gets or initializes the rating associated with the URL.
    /// </summary>
    public string Rating { get; init; }

    /// <summary>
    ///     Gets or initializes the provider of the URL.
    /// </summary>
    public string Provider { get; init; }

    /// <summary>
    ///     Gets the list of tags associated with the URL.
    /// </summary>
    public List<string> Tags { get; } = [];
}

/// <summary>
///     Represents a service for searching images.
/// </summary>
public class SearchImagesService : ISearchImagesService, INService
{
    private readonly SearchImageCacher cache;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<SearchImagesService> logger;

    /// <summary>
    ///     Initializes a new instance of the SearchImagesService class.
    /// </summary>
    /// <param name="http">The HTTP client factory for creating HttpClient instances.</param>
    /// <param name="cacher">The search image cacher.</param>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public SearchImagesService(
        IHttpClientFactory http,
        SearchImageCacher cacher,
        IDataConnectionFactory dbFactory, ILogger<SearchImagesService> logger)
    {
        var http1 = http.CreateClient();
        http1.AddFakeHeaders();
        cache = cacher;
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     Represents a collection of timers associated with auto-hentai functionality.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoHentaiTimers { get; } = new();

    /// <summary>
    ///     Represents a collection of timers associated with auto-boob functionality.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoBoobTimers { get; } = new();

    /// <summary>
    ///     Represents a collection of timers associated with auto-butt functionality.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoButtTimers { get; } = new();

    /// <summary>
    ///     Retrieves an NSFW image URL from Gelbooru.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public Task<UrlReply?> Gelbooru(ulong? guildId, bool forceExplicit, string[] tags)
    {
        return GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Gelbooru);
    }

    /// <summary>
    ///     Retrieves an NSFW image URL from Danbooru.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public Task<UrlReply?> Danbooru(ulong? guildId, bool forceExplicit, string[] tags)
    {
        return GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Danbooru);
    }

    /// <summary>
    ///     Retrieves an NSFW image URL from Konachan.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public Task<UrlReply?> Konachan(ulong? guildId, bool forceExplicit, string[] tags)
    {
        return GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Konachan);
    }

    /// <summary>
    ///     Retrieves an NSFW image URL from Yandere.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public Task<UrlReply?> Yandere(ulong? guildId, bool forceExplicit, string[] tags)
    {
        return GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Yandere);
    }

    /// <summary>
    ///     Retrieves an NSFW image URL from Rule34.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public Task<UrlReply?> Rule34(ulong? guildId, bool forceExplicit, string[] tags)
    {
        return GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Rule34);
    }

    /// <summary>
    ///     Retrieves an NSFW image URL from E621.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public Task<UrlReply?> E621(ulong? guildId, bool forceExplicit, string[] tags)
    {
        return GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.E621);
    }

    /// <summary>
    ///     Retrieves an NSFW image URL from DerpiBooru.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public Task<UrlReply?> DerpiBooru(ulong? guildId, bool forceExplicit, string[] tags)
    {
        return GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Derpibooru);
    }

    /// <summary>
    ///     Retrieves an NSFW image URL from SafeBooru.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public Task<UrlReply?> SafeBooru(ulong? guildId, bool forceExplicit, string[] tags)
    {
        return GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Safebooru);
    }

    /// <summary>
    ///     Retrieves an NSFW image URL from Sankaku.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public Task<UrlReply?> Sankaku(ulong? guildId, bool forceExplicit, string[] tags)
    {
        return GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Sankaku);
    }

    /// <summary>
    ///     Retrieves an NSFW image URL from RealBooru.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public Task<UrlReply?> RealBooru(ulong? guildId, bool forceExplicit, string[] tags)
    {
        return GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Realbooru);
    }

    /// <summary>
    ///     Retrieves an NSFW image URL from multiple providers, including Danbooru, Konachan, Gelbooru, and Yandere.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="forceExplicit">A boolean value indicating whether to force explicit content.</param>
    /// <param name="tags">An array of tags used to filter the image.</param>
    /// <returns>A task representing the asynchronous operation, containing the URL reply if successful; otherwise, null.</returns>
    public async Task<UrlReply?> Hentai(ulong? guildId, bool forceExplicit, string[] tags)
    {
        var providers = new[]
        {
            Booru.Danbooru, Booru.Konachan, Booru.Gelbooru, Booru.Yandere
        };

        using var cancelSource = new CancellationTokenSource();

        // create a task for each type
        var tasks = providers.Select(type => GetNsfwImageAsync(guildId, forceExplicit, tags, type, cancelSource.Token))
            .ToList();
        do
        {
            // wait for any of the tasks to complete
            var task = await Task.WhenAny(tasks).ConfigureAwait(false);

            // get its result
            var result = await task.ConfigureAwait(false);
            if (string.IsNullOrEmpty(result.Error))
            {
                // if we have a non-error result, cancel other searches and return the result
                await cancelSource.CancelAsync();
                return result;
            }

            // if the result is an error, remove that task from the waiting list,
            // and wait for another task to complete
            tasks.Remove(task);
        } while (tasks.Count > 0); // keep looping as long as there is any task remaining to be attempted

        // if we ran out of tasks, that means all tasks failed - return an error
        return new UrlReply
        {
            Error = "No hentai image found."
        };
    }

    /// <summary>
    ///     Toggles the blacklisting of a tag for the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="tag">The tag to toggle.</param>
    /// <returns>
    ///     A task representing the asynchronous operation, containing a boolean value indicating whether the tag was
    ///     added or removed.
    /// </returns>
    public async ValueTask<bool> ToggleBlacklistTag(ulong guildId, string tag)
    {
        var tagObj = new NsfwBlacklistedTag
        {
            GuildId = guildId, Tag = tag
        };

        bool added;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if the tag already exists for this guild
        var existing = await db.NsfwBlacklistedTags
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Tag == tag);

        if (existing == null)
        {
            // Tag doesn't exist, add it
            await db.InsertAsync(tagObj);
            added = true;
        }
        else
        {
            // Tag exists, remove it
            await db.NsfwBlacklistedTags
                .Where(x => x.GuildId == guildId && x.Tag == tag)
                .DeleteAsync();
            added = false;
        }

        return added;
    }

    /// <summary>
    ///     Gets the list of blacklisted tags for the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A task representing the asynchronous operation, containing an array of blacklisted tags.</returns>
    public async ValueTask<string[]?> GetBlacklistedTags(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var tags = await db.NsfwBlacklistedTags
            .Where(x => x.GuildId == guildId)
            .Select(x => x.Tag)
            .ToArrayAsync();

        return tags.Length != 0 ? tags : [];
    }

    private Task<UrlReply?> GetNsfwImageAsync(ulong? guildId, bool forceExplicit, string[]? tags, Booru dapi,
        CancellationToken cancel = default)
    {
        return GetNsfwImageAsync(guildId ?? 0, tags ?? [], forceExplicit, dapi, cancel);
    }

    private static bool IsValidTag(string tag)
    {
        return tag.All(x => x != '+' && x != '?' && x != '/');
        // tags mustn't contain + or ? or /
    }

    private async Task<UrlReply?> GetNsfwImageAsync(
        ulong guildId,
        string[] tags,
        bool forceExplicit,
        Booru dapi,
        CancellationToken cancel)
    {
        if (!tags.All(IsValidTag))
        {
            return new UrlReply
            {
                Error = "One or more tags are invalid.", Url = ""
            };
        }
#if DEBUG
        logger.LogInformation("Getting {V} image for Guild: {GuildId}...", dapi.ToString(), guildId);
#endif
        try
        {
            var blTags = await GetBlacklistedTags(guildId).ConfigureAwait(false);

            switch (dapi)
            {
                case Booru.E621:
                {
                    for (var i = 0; i < tags.Length; ++i)
                    {
                        if (tags[i] == "yuri")
                            tags[i] = "female/female";
                    }

                    break;
                }
                case Booru.Derpibooru:
                {
                    for (var i = 0; i < tags.Length; ++i)
                    {
                        if (tags[i] == "yuri")
                            tags[i] = "lesbian";
                    }

                    break;
                }
            }

            var result = await cache.GetImageNew(tags, forceExplicit, dapi, blTags?.ToHashSet() ?? [], cancel)
                .ConfigureAwait(false);

            if (result is null)
            {
                return new UrlReply
                {
                    Error = "Image not found.", Url = ""
                };
            }

            var reply = new UrlReply
            {
                Error = "", Url = result.FileUrl, Rating = result.Rating, Provider = result.SearchType.ToString()
            };

            reply.Tags.AddRange(result.Tags);

            return reply;
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("cancelled"))
                logger.LogError(ex, "Failed getting {Dapi} image: {Message}", dapi, ex.Message);
            return new UrlReply
            {
                Error = ex.Message, Url = ""
            };
        }
    }
}