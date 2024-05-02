using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest.Entities.Tracks;
using Mewdeko.Common.Configs;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SpotifyAPI.Web;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Music.CustomPlayer;

/// <summary>
/// Custom LavaLink player to be able to handle events and such, as well as auto play.
/// </summary>
public sealed class MewdekoPlayer : LavalinkPlayer
{
    private readonly IAudioService audioService;
    private readonly BotConfig config;
    private readonly IBotCredentials creds;
    private readonly HttpClient httpClient;
    private IDataCache cache;
    private IMessageChannel channel;
    private DiscordSocketClient client;
    private DbService dbService;
    private IBotStrings strings;

    /// <summary>
    /// Initializes a new instance of <see cref="MewdekoPlayer"/>.
    /// </summary>
    /// <param name="properties">The player properties.</param>
    public MewdekoPlayer(IPlayerProperties<MewdekoPlayer, MewdekoPlayerOptions> properties) : base(properties)
    {
        httpClient = properties.ServiceProvider.GetRequiredService<HttpClient>();
        config = properties.ServiceProvider.GetRequiredService<BotConfig>();
        audioService = properties.ServiceProvider.GetRequiredService<IAudioService>();
        creds = properties.ServiceProvider.GetRequiredService<IBotCredentials>();
        channel = properties.Options.Value.Channel;
        client = properties.ServiceProvider.GetRequiredService<DiscordSocketClient>();
        dbService = properties.ServiceProvider.GetRequiredService<DbService>();
        cache = properties.ServiceProvider.GetRequiredService<IDataCache>();
        strings = properties.ServiceProvider.GetRequiredService<IBotStrings>();
    }


    /// <summary>
    /// Handles the event the track ended, resolves stuff like auto play, auto playing the next track, and looping.
    /// </summary>
    /// <param name="item">The ended track.</param>
    /// <param name="reason">The reason the track ended.</param>
    /// <param name="token">The cancellation token.</param>
    protected override async ValueTask NotifyTrackEndedAsync(ITrackQueueItem item, TrackEndReason reason,
        CancellationToken token = default)
    {
        var musicChannel = await GetMusicChannel();
        var queue = await cache.GetMusicQueue(base.GuildId);
        var currentTrack = await cache.GetCurrentTrack(base.GuildId);
        var nextTrack = queue.FirstOrDefault(x => x.Index == currentTrack.Index + 1);
        switch (reason)
        {
            case TrackEndReason.Finished:
                var repeatType = await GetRepeatType();
                switch (repeatType)
                {
                    case PlayerRepeatType.None:

                        if (nextTrack is null)
                        {
                            await musicChannel.SendMessageAsync("Queue is empty. Stopping.");
                            await base.StopAsync(token);
                            await cache.SetCurrentTrack(base.GuildId, null);
                        }
                        else
                        {
                            await base.PlayAsync(nextTrack.Track, cancellationToken: token);
                            await cache.SetCurrentTrack(base.GuildId, nextTrack);
                        }

                        break;
                    case PlayerRepeatType.Track:
                        await base.PlayAsync(item.Track, cancellationToken: token);
                        break;
                    case PlayerRepeatType.Queue:
                        if (nextTrack is null)
                        {
                            await base.PlayAsync(queue[0].Track, cancellationToken: token);
                            await cache.SetCurrentTrack(base.GuildId, queue[0]);
                        }
                        else
                        {
                            await base.PlayAsync(nextTrack.Track, cancellationToken: token);
                            await cache.SetCurrentTrack(base.GuildId, nextTrack);
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                break;
            case TrackEndReason.LoadFailed:
                var failedEmbed = new EmbedBuilder()
                    .WithDescription($"Failed to load track {item.Track.Title}. Please try again.")
                    .WithOkColor()
                    .Build();
                await musicChannel.SendMessageAsync(embed: failedEmbed);
                break;
            case TrackEndReason.Stopped:
                return;
            case TrackEndReason.Replaced:
                break;
            case TrackEndReason.Cleanup:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
        }
    }

    /// <summary>
    /// Notifies the channel that a track has started playing.
    /// </summary>
    /// <param name="track">The track that started playing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem track,
        CancellationToken cancellationToken = new())
    {
        var queue = await cache.GetMusicQueue(base.GuildId);
        var currentTrack = await cache.GetCurrentTrack(base.GuildId);
        var musicChannel = await GetMusicChannel();
        await musicChannel.SendMessageAsync(embed: await PrettyNowPlayingAsync(queue));
        if (currentTrack.Index == queue.Count)
        {
            var success = await AutoPlay();
            if (!success)
            {
                await musicChannel.SendErrorAsync(strings.GetText("lastfm_credentials_invalid_autoplay"), config);
                await SetAutoPlay(0);
            }
        }
    }

    /// <summary>
    /// Gets the music channel for the player.
    /// </summary>
    /// <returns>The music channel for the player.</returns>
    public async Task<IMessageChannel?> GetMusicChannel()
    {
        var guildId = base.GuildId;
        await using var uow = dbService.GetDbContext();
        var settings = await uow.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (settings is null)
        {
            return channel;
        }

        var channelId = settings.MusicChannelId;
        return channelId.HasValue ? client.GetGuild(base.GuildId)?.GetTextChannel(channelId.Value) : this.channel;
    }

    /// <summary>
    /// Sets the music channel for the player.
    /// </summary>
    /// <param name="channelId">The channel id to set.</param>
    public async Task SetMusicChannelAsync(ulong channelId)
    {
        var guildId = base.GuildId;
        await using var uow = dbService.GetDbContext();
        var settings = await uow.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (settings is null)
        {
            settings = new MusicPlayerSettings
            {
                GuildId = base.GuildId, MusicChannelId = channelId
            };
            await uow.MusicPlayerSettings.AddAsync(settings);
        }
        else
        {
            settings.MusicChannelId = channelId;
        }

        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Gets a pretty now playing message for the player.
    /// </summary>
    public async Task<Embed> PrettyNowPlayingAsync(List<MewdekoTrack> queue)
    {
        var currentTrack = await cache.GetCurrentTrack(base.GuildId);
        var eb = new EmbedBuilder()
            .WithTitle(strings.GetText("music_now_playing"))
            .WithDescription($"`Artist:` ***{currentTrack.Track.Author}***" +
                             $"\n`Name:` ***[{currentTrack.Track.Title}]({currentTrack.Track.Uri})***" +
                             $"\n`Source:` ***{currentTrack.Track.Provider}***" +
                             $"\n`Queued By:` ***{currentTrack.Requester.Username}***")
            .WithOkColor()
            .WithImageUrl(currentTrack.Track.ArtworkUri?.ToString())
            .WithFooter(
                $"Track Number: {currentTrack.Index}/{queue.Count} | {base.Position.Value.Position:hh\\:mm\\:ss} | {base.CurrentTrack.Duration} | 🔊: {base.Volume * 100}% | 🔁: {await GetRepeatType()}");

        return eb.Build();
    }

    /// <summary>
    /// Contains logic for handling autoplay in a server. Requires either a last.fm API key.
    /// </summary>
    /// <returns>A bool depending on if the api key was correct.</returns>
    public async Task<bool> AutoPlay()
    {
        var autoPlay = await GetAutoPlay();
        if (autoPlay == 0)
            return true;
        var queue = await cache.GetMusicQueue(base.GuildId);
        var lastSong = queue.MaxBy(x => x.Index);
        if (lastSong is null)
            return true;

        LastFmResponse response = null;
        const int maxTries = 5;
        var tries = 0;
        var success = false;

        while (!success)
        {
            switch (tries)
            {
                case >= maxTries:
                    return true;
                case > 0:
                    lastSong = queue.FirstOrDefault(x => x.Index == queue.Count - tries);
                    break;
            }

            // sorted info for attempting to fetch data from lastfm
            var fullTitle = lastSong.Track.Title;
            var trackTitle = fullTitle;
            var artistName = lastSong.Track.Author;
            var hyphenIndex = fullTitle.IndexOf(" - ", StringComparison.Ordinal);

            // if the title has a hyphen, split the title and artist, used in cases where the title is formatted as "Artist - Title"
            if (hyphenIndex != -1)
            {
                artistName = fullTitle.Substring(0, hyphenIndex).Trim();
                trackTitle = fullTitle.Substring(hyphenIndex + 3).Trim();
            }

            // remove any extra info from the title that might be in brackets or parentheses
            trackTitle = Regex.Replace(trackTitle, @"\s*\[.*?\]\s*", "", RegexOptions.Compiled);
            trackTitle = Regex.Replace(trackTitle, @"\s*\([^)]*\)\s*", "", RegexOptions.Compiled);
            trackTitle = trackTitle.Trim();

            // Query lastfm the first time with the formatted track title that doesnt contain the artist, with artist data from the track itself
            var apiResponse = await httpClient.GetStringAsync(
                $"http://ws.audioscrobbler.com/2.0/?method=track.getsimilar&artist={Uri.EscapeDataString(artistName)}&track={Uri.EscapeDataString(trackTitle)}&autocorrect=1&api_key={Uri.EscapeDataString(creds.LastFmApiKey)}&format=json");
            response = JsonConvert.DeserializeObject<LastFmResponse>(apiResponse);

            // If the response is null, the api returned an error, try again
            if (response.Similartracks is null)
            {
                tries++;
                continue;
            }

            // If the first query returns no results, assume that the title had useful author info, use the split title and author info and try again
            if (response.Similartracks.Track.Count == 0)
            {
                apiResponse = await httpClient.GetStringAsync(
                    $"http://ws.audioscrobbler.com/2.0/?method=track.getsimilar&artist={Uri.EscapeDataString(lastSong.Track.Author)}&track={Uri.EscapeDataString(trackTitle)}&autocorrect=1&api_key={Uri.EscapeDataString(creds.LastFmApiKey)}&format=json");
                response = JsonConvert.DeserializeObject<LastFmResponse>(apiResponse);
            }

            if (response.Similartracks.Track.Count != 0)
                success = true;

            tries++;
        }

        // If the response is empty, return true
        if (response.Similartracks.Track.Count == 0)
            return true;

        var queuedTrackNames = new HashSet<string>(queue.Select(q => q.Track.Title));

        // Filter out tracks that are already in the queue
        var filteredTracks = response.Similartracks.Track
            .Where(t => !queuedTrackNames.Contains($"{t.Name}"))
            .ToList();

        // get the amount of tracks to take, either the amount of tracks in the response or the amount of tracks to autoplay
        var toTake = Math.Min(autoPlay, filteredTracks.Count);

        foreach (var rec in filteredTracks.Take(toTake))
        {
            var trackToLoad =
                await audioService.Tracks.LoadTrackAsync($"{rec.Name} {rec.Artist.Name}", TrackSearchMode.YouTube);
            if (trackToLoad is null)
                continue;
            queue.Add(new MewdekoTrack(queue.Count + 1, trackToLoad, new PartialUser()
            {
                AvatarUrl = client.CurrentUser.GetAvatarUrl(), Username = "Mewdeko", Id = client.CurrentUser.Id
            }));
            await cache.SetMusicQueue(base.GuildId, queue);
        }

        await cache.SetMusicQueue(base.GuildId, queue);
        return true;
    }

    /// <summary>
    /// Gets the volume for a guild, defaults to max.
    /// </summary>
    /// <returns>An integer representing the guilds player volume</returns>
    public async Task<int> GetVolume()
    {
        var guildId = base.GuildId;
        await using var uow = dbService.GetDbContext();
        var settings = await uow.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        return settings?.Volume ?? 100;
    }

    /// <summary>
    /// Sets the volume for the player.
    /// </summary>
    /// <param name="volume">The volume to set.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetGuildVolumeAsync(int volume)
    {
        var guildId = base.GuildId;
        await using var uow = dbService.GetDbContext();
        var settings = await uow.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (settings is null)
        {
            settings = new MusicPlayerSettings
            {
                GuildId = base.GuildId, Volume = volume
            };
            await uow.MusicPlayerSettings.AddAsync(settings);
        }
        else
        {
            settings.Volume = volume;
        }

        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Gets the repeat type for the player.
    /// </summary>
    /// <returns>A <see cref="PlayerRepeatType"/> for the guild.</returns>
    public async Task<PlayerRepeatType> GetRepeatType()
    {
        var guildId = base.GuildId;
        await using var uow = dbService.GetDbContext();
        var settings = await uow.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        return settings?.PlayerRepeat ?? PlayerRepeatType.Queue;
    }

    /// <summary>
    /// Sets the repeat type for the player.
    /// </summary>
    /// <param name="repeatType">The repeat type to set.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetRepeatTypeAsync(PlayerRepeatType repeatType)
    {
        var guildId = base.GuildId;
        await using var uow = dbService.GetDbContext();
        var settings = await uow.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (settings is null)
        {
            settings = new MusicPlayerSettings
            {
                GuildId = base.GuildId, PlayerRepeat = repeatType
            };
            await uow.MusicPlayerSettings.AddAsync(settings);
        }
        else
        {
            settings.PlayerRepeat = repeatType;
        }

        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Gets the autoplay number for a guild, usually off.
    /// </summary>
    public async Task<int> GetAutoPlay()
    {
        var guildId = base.GuildId;
        await using var uow = dbService.GetDbContext();
        var settings = await uow.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        return settings?.AutoPlay ?? 0;
    }

    /// <summary>
    /// Sets the autoplay amount for the guild.
    /// </summary>
    /// <param name="autoPlay">The amount of songs to autoplay.</param>
    public async Task SetAutoPlay(int autoPlay)
    {
        var guildId = base.GuildId;
        await using var uow = dbService.GetDbContext();
        var settings = await uow.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (settings is null)
        {
            settings = new MusicPlayerSettings
            {
                GuildId = base.GuildId, AutoPlay = autoPlay
            };
            await uow.MusicPlayerSettings.AddAsync(settings);
        }
        else
        {
            settings.AutoPlay = autoPlay;
        }

        await uow.SaveChangesAsync();
    }

    private async Task<SpotifyClient> GetSpotifyClient()
    {
        var spotifyClientConfig = SpotifyClientConfig.CreateDefault();
        var request =
            new ClientCredentialsRequest(creds.SpotifyClientId, creds.SpotifyClientSecret);
        var response = await new OAuthClient(spotifyClientConfig).RequestToken(request).ConfigureAwait(false);
        return new SpotifyClient(spotifyClientConfig.WithToken(response.AccessToken));
    }
}