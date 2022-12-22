using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Mewdeko.Modules.Nsfw.Common;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Nsfw;

public record UrlReply
{
    public string Error { get; init; }
    public string Url { get; init; }
    public string Rating { get; init; }
    public string Provider { get; init; }
    public List<string> Tags { get; } = new();
}

public class SearchImagesService : ISearchImagesService, INService
{
    private readonly HttpClient http;
    private readonly SearchImageCacher cache;
    private readonly DbService db;
    private readonly ConcurrentDictionary<ulong, HashSet<string>> blacklistedTags;

    public ConcurrentDictionary<ulong, Timer> AutoHentaiTimers { get; } = new();
    public ConcurrentDictionary<ulong, Timer> AutoBoobTimers { get; } = new();
    public ConcurrentDictionary<ulong, Timer> AutoButtTimers { get; } = new();

    public SearchImagesService(
        IHttpClientFactory http,
        SearchImageCacher cacher, DiscordSocketClient client,
        DbService db)
    {
        this.http = http.CreateClient();
        this.http.AddFakeHeaders();
        cache = cacher;
        this.db = db;
        using var uow = db.GetDbContext();
        var gc = uow.GuildConfigs.Include(x => x.NsfwBlacklistedTags).Where(x => client.Guilds.Select(x => x.Id).Contains(x.GuildId));
        blacklistedTags = new ConcurrentDictionary<ulong, HashSet<string>>(
            gc.ToDictionary(
                x => x.GuildId,
                x => new HashSet<string>(x.NsfwBlacklistedTags.Select(y => y.Tag))));
    }

    private Task<UrlReply?> GetNsfwImageAsync(ulong? guildId, bool forceExplicit, string[]? tags, Booru dapi, CancellationToken cancel = default) =>
        GetNsfwImageAsync(guildId ?? 0, tags ?? Array.Empty<string>(), forceExplicit, dapi, cancel);

    private static bool IsValidTag(string tag) => tag.All(x => x != '+' && x != '?' && x != '/'); // tags mustn't contain + or ? or /

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
        Log.Information("Getting {V} image for Guild: {GuildId}...", dapi.ToString(), guildId);
#endif
        try
        {
            blacklistedTags.TryGetValue(guildId, out var blTags);

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

            var result = await cache.GetImageNew(tags, forceExplicit, dapi, blTags ?? new HashSet<string>(), cancel)
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
                Log.Error(ex, "Failed getting {Dapi} image: {Message}", dapi, ex.Message);
            return new UrlReply
            {
                Error = ex.Message, Url = ""
            };
        }
    }

    public Task<UrlReply?> Gelbooru(ulong? guildId, bool forceExplicit, string[] tags)
        => GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Gelbooru);

    public Task<UrlReply?> Danbooru(ulong? guildId, bool forceExplicit, string[] tags)
        => GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Danbooru);

    public Task<UrlReply?> Konachan(ulong? guildId, bool forceExplicit, string[] tags)
        => GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Konachan);

    public Task<UrlReply?> Yandere(ulong? guildId, bool forceExplicit, string[] tags)
        => GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Yandere);

    public Task<UrlReply?> Rule34(ulong? guildId, bool forceExplicit, string[] tags)
        => GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Rule34);

    public Task<UrlReply?> E621(ulong? guildId, bool forceExplicit, string[] tags)
        => GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.E621);

    public Task<UrlReply?> DerpiBooru(ulong? guildId, bool forceExplicit, string[] tags)
        => GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Derpibooru);

    public Task<UrlReply?> SafeBooru(ulong? guildId, bool forceExplicit, string[] tags)
        => GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Safebooru);

    public Task<UrlReply?> Sankaku(ulong? guildId, bool forceExplicit, string[] tags)
        => GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Sankaku);

    public Task<UrlReply?> RealBooru(ulong? guildId, bool forceExplicit, string[] tags)
        => GetNsfwImageAsync(guildId, forceExplicit, tags, Booru.Realbooru);

    public async Task<UrlReply?> Hentai(ulong? guildId, bool forceExplicit, string[] tags)
    {
        var providers = new[]
        {
            Booru.Danbooru, Booru.Konachan, Booru.Gelbooru, Booru.Yandere
        };

        using var cancelSource = new CancellationTokenSource();

        // create a task for each type
        var tasks = providers.Select(type => GetNsfwImageAsync(guildId, forceExplicit, tags, type, cancelSource.Token)).ToList();
        do
        {
            // wait for any of the tasks to complete
            var task = await Task.WhenAny(tasks).ConfigureAwait(false);

            // get its result
            var result = task.GetAwaiter().GetResult();
            if (string.IsNullOrEmpty(result.Error))
            {
                // if we have a non-error result, cancel other searches and return the result
                cancelSource.Cancel();
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


    private readonly object taglock = new();

    public async ValueTask<bool> ToggleBlacklistTag(ulong guildId, string tag)
    {
        var tagObj = new NsfwBlacklitedTag
        {
            Tag = tag
        };

        bool added;
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set.Include(y => y.NsfwBlacklistedTags));
        if (gc.NsfwBlacklistedTags.Add(tagObj))
        {
            added = true;
        }
        else
        {
            gc.NsfwBlacklistedTags.Remove(tagObj);
            var toRemove = gc.NsfwBlacklistedTags.FirstOrDefault(x => x.Equals(tagObj));
            if (toRemove != null)
                uow.Remove(toRemove);
            added = false;
        }

        var newTags = new HashSet<string>(gc.NsfwBlacklistedTags.Select(x => x.Tag));
        blacklistedTags.AddOrUpdate(guildId, newTags, delegate { return newTags; });

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return await new ValueTask<bool>(added);
    }

    public ValueTask<string[]> GetBlacklistedTags(ulong guildId)
    {
        lock (taglock)
        {
            return blacklistedTags.TryGetValue(guildId, out var tags) ? new ValueTask<string[]>(tags.ToArray()) : new ValueTask<string[]>(Array.Empty<string>());
        }
    }
}