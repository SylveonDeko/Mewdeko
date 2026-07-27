using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using DataModel;
using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Music.Services;

/// <summary>
///     Listens for music links posted in configured channels and replies with an embed
///     containing the track's title, artist, and artwork alongside equivalent links across
///     Apple Music, Spotify, YouTube Music, and other providers, resolved via the song.link
///     (Odesli) API. Falls back to a direct Lavalink search for Spotify, YouTube, and YouTube
///     Music when song.link has no match for one of those platforms.
/// </summary>
public partial class MusicLinkService : INService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly (string Platform, string DisplayName)[] PlatformDisplayOrder =
    [
        ("appleMusic", "Apple Music"),
        ("spotify", "Spotify"),
        ("youtubeMusic", "YouTube Music"),
        ("youtube", "YouTube"),
        ("amazonMusic", "Amazon Music"),
        ("deezer", "Deezer"),
        ("tidal", "Tidal"),
        ("soundcloud", "SoundCloud"),
        ("napster", "Napster"),
        ("pandora", "Pandora"),
        ("yandex", "Yandex Music"),
        ("audiomack", "Audiomack"),
        ("anghami", "Anghami"),
        ("boomplay", "Boomplay")
    ];

    private readonly IAudioService audioService;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler handler;
    private readonly IHttpClientFactory httpFactory;
    private readonly ILogger<MusicLinkService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MusicLinkService" /> class and subscribes to
    ///     the message received event to watch configured channels for music links.
    /// </summary>
    /// <param name="handler">The event handler service used to subscribe to Discord gateway events.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="httpFactory">The HTTP client factory used to query the song.link API.</param>
    /// <param name="audioService">
    ///     The Lavalink audio service, used to search Spotify/YouTube/YouTube Music (via the LavaSrc
    ///     plugin) directly when song.link has no match.
    /// </param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public MusicLinkService(EventHandler handler, IDataConnectionFactory dbFactory, IHttpClientFactory httpFactory,
        IAudioService audioService, ILogger<MusicLinkService> logger)
    {
        this.handler = handler;
        this.dbFactory = dbFactory;
        this.httpFactory = httpFactory;
        this.audioService = audioService;
        this.logger = logger;

        handler.Subscribe("MessageReceived", "MusicLinkService", OnMessageReceived);
    }

    /// <summary>
    ///     Unsubscribes from the message received event.
    /// </summary>
    public void Dispose()
    {
        handler.Unsubscribe("MessageReceived", "MusicLinkService", OnMessageReceived);
    }

    /// <summary>
    ///     Enables music link conversion for the given channel in the given guild.
    /// </summary>
    /// <param name="guildId">The guild the channel belongs to.</param>
    /// <param name="channelId">The channel to enable.</param>
    /// <returns><see langword="true" /> if the channel was newly enabled.</returns>
    public async Task<bool> EnableChannelAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.MusicLinkChannels
            .AnyAsync(x => x.GuildId == guildId && x.ChannelId == channelId);

        if (exists)
            return false;

        await db.InsertAsync(new MusicLinkChannel
        {
            GuildId = guildId, ChannelId = channelId
        });

        return true;
    }

    /// <summary>
    ///     Disables music link conversion for the given channel in the given guild.
    /// </summary>
    /// <param name="guildId">The guild the channel belongs to.</param>
    /// <param name="channelId">The channel to disable.</param>
    /// <returns><see langword="true" /> if a channel was removed.</returns>
    public async Task<bool> DisableChannelAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.MusicLinkChannels
            .Where(x => x.GuildId == guildId && x.ChannelId == channelId)
            .DeleteAsync();

        return deleted > 0;
    }

    /// <summary>
    ///     Gets all channels configured for music link conversion in the given guild.
    /// </summary>
    /// <param name="guildId">The guild to look up.</param>
    /// <returns>The list of configured channel IDs.</returns>
    public async Task<List<ulong>> GetChannelsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.MusicLinkChannels
            .Where(x => x.GuildId == guildId)
            .Select(x => x.ChannelId)
            .ToListAsync();
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        if (message is not IUserMessage userMessage || message.Author.IsBot)
            return;

        if (userMessage.Channel is not ITextChannel channel)
            return;

        var url = ExtractMusicLink(userMessage.Content);
        if (url is null)
            return;

        await using var db = await dbFactory.CreateConnectionAsync();
        var enabled = await db.MusicLinkChannels
            .AnyAsync(x => x.GuildId == channel.Guild.Id && x.ChannelId == channel.Id);

        if (!enabled)
            return;

        try
        {
            var links = await ResolveLinksAsync(url);
            if (links is null)
                return;

            var components = await BuildComponentsAsync(links).ConfigureAwait(false);
            if (components is null)
                return;

            await channel.SendMessageAsync(components: components, flags: MessageFlags.ComponentsV2,
                    messageReference: new MessageReference(userMessage.Id), allowedMentions: AllowedMentions.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve music link {Url} in channel {ChannelId}", url, channel.Id);
        }
    }

    private static string? ExtractMusicLink(string content)
    {
        foreach (Match match in UrlRegex().Matches(content))
        {
            if (!Uri.TryCreate(match.Value, UriKind.Absolute, out var uri))
                continue;

            var host = uri.Host.ToLowerInvariant();

            var isMusicLink = host.Contains("music.apple.com")
                              || host.Contains("open.spotify.com")
                              || host.Contains("music.youtube.com")
                              || host.Contains("deezer.com")
                              || host.Contains("tidal.com")
                              || host.Contains("music.amazon.")
                              || host.Contains("soundcloud.com")
                              || host.Contains("pandora.com")
                              || host.Contains("youtube.com") && uri.AbsolutePath.StartsWith("/watch")
                              || host.Contains("youtu.be");

            if (isMusicLink)
                return match.Value;
        }

        return null;
    }

    private async Task<OdesliResponse?> ResolveLinksAsync(string url)
    {
        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mewdeko/1.0 (+https://mewdeko.tech)");

        var requestUrl = $"https://api.song.link/v1-alpha.1/links?url={Uri.EscapeDataString(url)}";

        using var response = await http.GetAsync(requestUrl).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("song.link lookup for {Url} failed with status {Status}", url,
                response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<OdesliResponse>(stream, JsonOptions).ConfigureAwait(false);
    }

    private async Task<MessageComponent?> BuildComponentsAsync(OdesliResponse response)
    {
        if (response.EntitiesByUniqueId is null || response.EntitiesByUniqueId.Count == 0)
            return null;

        var entity = response.EntityUniqueId is not null &&
                     response.EntitiesByUniqueId.TryGetValue(response.EntityUniqueId, out var matched)
            ? matched
            : response.EntitiesByUniqueId.Values.First();

        var links = new Dictionary<string, string>();
        if (response.LinksByPlatform is not null)
        {
            foreach (var (platform, _) in PlatformDisplayOrder)
            {
                if (response.LinksByPlatform.TryGetValue(platform, out var link) &&
                    !string.IsNullOrWhiteSpace(link.Url))
                {
                    links[platform] = link.Url;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(entity.Title))
        {
            var query = string.IsNullOrWhiteSpace(entity.ArtistName)
                ? entity.Title
                : $"{entity.ArtistName} {entity.Title}";

            await FillMissingLinkAsync(links, "spotify", query).ConfigureAwait(false);
            await FillMissingLinkAsync(links, "youtubeMusic", query).ConfigureAwait(false);
            await FillMissingLinkAsync(links, "youtube", query).ConfigureAwait(false);
        }

        var containerComponents = new List<IMessageComponentBuilder>
        {
            new TextDisplayBuilder($"# {entity.Title ?? "Unknown Track"}")
        };

        if (!string.IsNullOrWhiteSpace(entity.ArtistName))
            containerComponents.Add(new TextDisplayBuilder($"-# {entity.ArtistName}"));

        if (!string.IsNullOrWhiteSpace(entity.ThumbnailUrl))
        {
            containerComponents.Add(new MediaGalleryBuilder()
                .WithItems([
                    new MediaGalleryItemProperties(new UnfurledMediaItemProperties
                    {
                        Url = entity.ThumbnailUrl
                    })
                ]));
        }

        if (links.Count > 0)
        {
            containerComponents.Add(new SeparatorBuilder());

            ActionRowBuilder? currentRow = null;
            var inRow = 0;
            var rows = 0;

            foreach (var (platform, displayName) in PlatformDisplayOrder)
            {
                if (!links.TryGetValue(platform, out var url))
                    continue;

                if (currentRow is null || inRow == 4)
                {
                    if (rows == 5)
                        break;

                    currentRow = new ActionRowBuilder();
                    containerComponents.Add(currentRow);
                    inRow = 0;
                    rows++;
                }

                currentRow.WithButton(displayName, style: ButtonStyle.Link, url: url);
                inRow++;
            }
        }

        var mainContainer = new ContainerBuilder()
            .WithComponents(containerComponents)
            .WithAccentColor(Mewdeko.OkColor);

        return new ComponentBuilderV2()
            .AddComponent(mainContainer)
            .Build();
    }

    private async Task FillMissingLinkAsync(Dictionary<string, string> links, string platform, string query)
    {
        if (links.ContainsKey(platform))
            return;

        try
        {
            var mode = platform switch
            {
                "spotify" => TrackSearchMode.Spotify,
                "youtubeMusic" => TrackSearchMode.YouTubeMusic,
                "youtube" => TrackSearchMode.YouTube,
                _ => (TrackSearchMode?)null
            };

            if (mode is null)
                return;

            var track = await audioService.Tracks.LoadTrackAsync(query, mode.Value).ConfigureAwait(false);
            if (track?.Uri is null)
                return;

            links[platform] = platform == "youtubeMusic"
                ? ToYouTubeMusicUrl(track.Uri)
                : track.Uri.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to auto-search {Platform} for {Query}", platform, query);
        }
    }

    private static string ToYouTubeMusicUrl(Uri uri)
    {
        var videoId = uri.Host.Contains("youtu.be")
            ? uri.AbsolutePath.Trim('/')
            : HttpUtility.ParseQueryString(uri.Query)["v"];

        return string.IsNullOrWhiteSpace(videoId)
            ? uri.ToString()
            : $"https://music.youtube.com/watch?v={videoId}";
    }

    [GeneratedRegex(@"https?:\/\/[^\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    private class OdesliResponse
    {
        [JsonPropertyName("entityUniqueId")]
        public string? EntityUniqueId { get; init; }

        [JsonPropertyName("entitiesByUniqueId")]
        public Dictionary<string, OdesliEntity>? EntitiesByUniqueId { get; init; }

        [JsonPropertyName("linksByPlatform")]
        public Dictionary<string, OdesliLink>? LinksByPlatform { get; init; }
    }

    private class OdesliEntity
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; init; }

        [JsonPropertyName("thumbnailUrl")]
        public string? ThumbnailUrl { get; init; }
    }

    private class OdesliLink
    {
        [JsonPropertyName("url")]
        public string? Url { get; init; }
    }
}