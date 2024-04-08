using Lavalink4NET;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Mewdeko.Common.Configs;
using Mewdeko.Modules.Music.Common;
using SpotifyAPI.Web;

namespace Mewdeko.Modules.Music.Services;

/// <summary>
/// Service for music playback.
/// </summary>
public class MusicService : INService
{
    private readonly ConcurrentDictionary<ulong, List<LavalinkTrack?>> Queues;
    private readonly ConcurrentDictionary<ulong, MusicPlayerSettings> settings;
    private readonly DbService db;
    private readonly LavalinkNode lavaNode;
    private readonly IBotCredentials creds;
    private readonly IGoogleApiService googleApi;
    private readonly DiscordSocketClient client;
    private readonly BotConfig config;

    /// <summary>
    /// Initializes a new instance of <see cref="MusicService"/>.
    /// </summary>
    /// <param name="lavaNode">The Lavalink node</param>
    /// <param name="creds">The bot credentials</param>
    /// <param name="db">The database service</param>
    /// <param name="eventHandler">The event handler</param>
    /// <param name="googleApi">The Google API service</param>
    /// <param name="client">The Discord client</param>
    /// <param name="config">The bot configuration service</param>
    public MusicService(LavalinkNode lavaNode, IBotCredentials creds, DbService db, EventHandler eventHandler,
        IGoogleApiService googleApi,
        DiscordSocketClient client,
        BotConfig config)
    {
        this.lavaNode = lavaNode;
        this.creds = creds;
        this.db = db;
        this.googleApi = googleApi;
        this.client = client;
        this.config = config;
        settings = new ConcurrentDictionary<ulong, MusicPlayerSettings>();
        Queues = new ConcurrentDictionary<ulong, List<LavalinkTrack>>();
        eventHandler.UserVoiceStateUpdated += HandleDisconnect;
    }

    /// <summary>
    /// Gets pretty information about the current track.
    /// </summary>
    /// <param name="player">The player</param>
    /// <param name="guild">The guild to get information from</param>
    /// <returns></returns>
    public async Task<string> GetPrettyInfo(LavalinkPlayer player, IGuild guild)
    {
        var currentTrack = player.CurrentTrack;
        var currentContext = currentTrack.Context as AdvancedTrackContext;
        var musicSettings = await GetSettingsInternalAsync(guild.Id).ConfigureAwait(false);
        return
            $@"{player.Position.Position:hh\:mm\:ss}/{currentTrack.Duration:hh\:mm\:ss} | {currentContext.QueueUser} | {currentContext.QueuedPlatform} | Vol: {musicSettings.Volume} | Loop: {musicSettings.PlayerRepeat} | {GetQueue(guild.Id).Count} tracks in queue";
    }

    /// <summary>
    /// Updates the default playlist for a user.
    /// </summary>
    /// <param name="user">The user to update the playlist for</param>
    /// <param name="mpl">The playlist to update</param>
    public async Task UpdateDefaultPlaylist(IUser user, MusicPlaylist mpl)
    {
        await using var uow = db.GetDbContext();
        var def = await uow.MusicPlaylists.GetDefaultPlaylist(user.Id);
        if (def != null)
        {
            var toupdate = new MusicPlaylist
            {
                AuthorId = def.AuthorId,
                Author = def.Author,
                DateAdded = def.DateAdded,
                Id = def.Id,
                IsDefault = false,
                Name = def.Name,
                Songs = def.Songs
            };
            uow.MusicPlaylists.Update(toupdate);
        }

        var toupdate1 = new MusicPlaylist
        {
            AuthorId = mpl.AuthorId,
            Author = mpl.Author,
            DateAdded = mpl.DateAdded,
            Id = mpl.Id,
            IsDefault = true,
            Name = mpl.Name,
            Songs = mpl.Songs
        };
        uow.MusicPlaylists.Update(toupdate1);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a song to the current queue.
    /// </summary>
    /// <param name="guildId">The guild id to queue tracks for</param>
    /// <param name="user">The user who queued the track</param>
    /// <param name="lavaTracks">The tracks to queue</param>
    /// <param name="queuedPlatform">The platform the tracks are queued from</param>
    /// <returns></returns>
    public Task Enqueue(
        ulong guildId,
        IUser user,
        IEnumerable<LavalinkTrack> lavaTracks,
        Platform queuedPlatform = Platform.Youtube)
    {
        var queue = Queues.GetOrAdd(guildId, new List<LavalinkTrack>());
        foreach (var i in lavaTracks)
        {
            i.Context = new AdvancedTrackContext(user, queuedPlatform);
            queue.Add(i);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a song to the current queue.
    /// </summary>
    /// <param name="guildId">The guild id to queue tracks for</param>
    /// <param name="user">The user who queued the track</param>
    /// <param name="lavaTrack">The track to queue</param>
    /// <param name="queuedPlatform">The platform the track is queued from</param>
    /// <returns></returns>
    public Task Enqueue(
        ulong guildId,
        IUser user,
        LavalinkTrack? lavaTrack,
        Platform queuedPlatform = Platform.Youtube)
    {
        var queue = Queues.GetOrAdd(guildId, new List<LavalinkTrack>());
        lavaTrack.Context = new AdvancedTrackContext(user, queuedPlatform);
        queue.Add(lavaTrack);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Service for trying to queue a spotify playlist, album or track.
    /// </summary>
    /// <param name="guild">The guild to queue the track for</param>
    /// <param name="user">The user who queued the track</param>
    /// <param name="chan">The channel to send messages to</param>
    /// <param name="player">The player to queue the track for</param>
    /// <param name="uri">The uri to queue</param>
    public async Task SpotifyQueue(
        IGuild guild,
        IUser user,
        ITextChannel? chan,
        LavalinkPlayer player,
        string? uri)
    {
        var spotifyUrl = new Uri(uri);
        switch (spotifyUrl.Segments[1])
        {
            case "playlist/":
                if (creds.SpotifyClientId is null or "")
                {
                    await chan.SendErrorAsync(
                            "Looks like the owner of this bot hasnt added the spotify Id and CLient Secret to their credentials. Spotify queueing wont work without this.",
                            config: config)
                        .ConfigureAwait(false);
                    return;
                }

                var result = await (await GetSpotifyClient().ConfigureAwait(false)).Playlists
                    .Get(spotifyUrl.Segments[2]).ConfigureAwait(false);
                if (result.Tracks != null && result.Tracks.Items!.Count > 0)
                {
                    var items = result.Tracks.Items;
                    var eb = new EmbedBuilder()
                        .WithAuthor("Spotify Playlist",
                            "https://assets.stickpng.com/images/5ece5029123d6d0004ce5f8b.png").WithOkColor()
                        .WithDescription($"Trying to queue {items!.Count} tracks from {result.Name}...")
                        .WithThumbnailUrl(result.Images?.FirstOrDefault()?.Url);
                    var msg = await chan!.SendMessageAsync(embed: eb.Build(),
                        components: config.ShowInviteButton
                            ? new ComponentBuilder()
                                .WithButton(label: "Support Server", style: ButtonStyle.Link,
                                    url: "https://discord.gg/mewdeko")
                                .WithButton(label: "Support Us!", style: ButtonStyle.Link,
                                    url: "https://ko-fi.com/mewdeko")
                                .Build()
                            : null);
                    var addedcount = 0;
                    foreach (var track in items.Select(i => i.Track as FullTrack))
                    {
                        if (player.State is PlayerState.Destroyed or PlayerState.NotConnected)
                            return;
                        var lavaTrack = await lavaNode.GetTrackAsync(
                                $"{track?.Name} {track?.Artists.FirstOrDefault()?.Name}",
                                !config.YoutubeSupport ? SearchMode.SoundCloud : SearchMode.YouTube)
                            .ConfigureAwait(false);
                        if (lavaTrack is null) continue;
                        await Enqueue(guild.Id, user, lavaTrack, Platform.Spotify).ConfigureAwait(false);
                        if (player.State != PlayerState.Playing && player.State != PlayerState.Destroyed)
                        {
                            await player.PlayAsync(lavaTrack).ConfigureAwait(false);
                            await player.SetVolumeAsync(await GetVolume(guild.Id).ConfigureAwait(false) / 100.0F)
                                .ConfigureAwait(false);
                            await ModifySettingsInternalAsync(guild.Id,
                                    (musicPlayerSettings, _) => musicPlayerSettings.MusicChannelId = chan.Id, chan.Id)
                                .ConfigureAwait(false);
                        }

                        addedcount++;
                    }

                    if (addedcount == 0)
                    {
                        eb.WithErrorColor()
                            .WithDescription(
                                $"Seems like I couldn't load any tracks from {result.Name}... Perhaps its private?");
                        await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
                    }

                    eb.WithDescription($"Successfully queued {addedcount} tracks!");
                    await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
                }

                break;
            case "album/":
                if (string.IsNullOrEmpty(creds.SpotifyClientId))
                {
                    await chan.SendErrorAsync(
                            "Looks like the owner of this bot hasnt added the spotify Id and CLient Secret to their credentials. Spotify queueing wont work without this.",
                            config)
                        .ConfigureAwait(false);
                    return;
                }

                var result1 = await (await GetSpotifyClient().ConfigureAwait(false)).Albums.Get(spotifyUrl.Segments[2])
                    .ConfigureAwait(false);
                if (result1.Tracks.Items.Any())
                {
                    var items = result1.Tracks.Items;
                    var eb = new EmbedBuilder()
                        .WithAuthor("Spotify Album",
                            "https://assets.stickpng.com/images/5ece5029123d6d0004ce5f8b.png").WithOkColor()
                        .WithDescription($"Trying to queue {items.Count} tracks from {result1.Name}...")
                        .WithThumbnailUrl(result1.Images.FirstOrDefault()?.Url);
                    var msg = await chan!.SendMessageAsync(embed: eb.Build(),
                        components: config.ShowInviteButton
                            ? new ComponentBuilder()
                                .WithButton(label: "Support Server", style: ButtonStyle.Link,
                                    url: "https://discord.gg/mewdeko")
                                .WithButton(label: "Support Us!", style: ButtonStyle.Link,
                                    url: "https://ko-fi.com/mewdeko")
                                .Build()
                            : null);
                    var addedcount = 0;
                    foreach (var track in items)
                    {
                        if (player.State is PlayerState.Destroyed or PlayerState.NotConnected)
                            return;
                        var lavaTrack = await lavaNode.GetTrackAsync(
                                $"{track.Name} {track.Artists.FirstOrDefault()?.Name}",
                                !config.YoutubeSupport ? SearchMode.SoundCloud : SearchMode.YouTube)
                            .ConfigureAwait(false);
                        if (lavaTrack is null) continue;
                        await Enqueue(guild.Id, user, lavaTrack, Platform.Spotify).ConfigureAwait(false);
                        if (player.State != PlayerState.Playing)
                        {
                            await player.PlayAsync(lavaTrack).ConfigureAwait(false);
                            await player.SetVolumeAsync(await GetVolume(guild.Id).ConfigureAwait(false) / 100.0F)
                                .ConfigureAwait(false);
                            await ModifySettingsInternalAsync(guild.Id,
                                    (musicPlayerSettings, _) => musicPlayerSettings.MusicChannelId = chan.Id, chan.Id)
                                .ConfigureAwait(false);
                        }

                        addedcount++;
                    }

                    if (addedcount == 0)
                    {
                        eb.WithErrorColor()
                            .WithDescription(
                                $"Seems like I couldn't load any tracks from {result1.Name}... Perhaps the songs weren't found or are exclusive?");
                        await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
                    }

                    eb.WithDescription($"Successfully queued {addedcount} tracks!").WithTitle(result1.Name);
                    await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
                }

                break;

            case "track/":
                if (string.IsNullOrEmpty(creds.SpotifyClientId))
                {
                    await chan.SendErrorAsync(
                            "Looks like the owner of this bot hasnt added the spotify Id and CLient Secret to their credentials. Spotify queueing wont work without this.",
                            config)
                        .ConfigureAwait(false);
                    return;
                }

                var result3 = await (await GetSpotifyClient().ConfigureAwait(false)).Tracks.Get(spotifyUrl.Segments[2])
                    .ConfigureAwait(false);
                if (string.IsNullOrEmpty(result3.Name))
                {
                    await chan.SendErrorAsync(
                            "Seems like i can't find or play this. Please try with a different link!", config)
                        .ConfigureAwait(false);
                    return;
                }

                var lavaTrack3 = await lavaNode.GetTrackAsync(
                        $"{result3.Name} {result3.Artists.FirstOrDefault()?.Name}",
                        !config.YoutubeSupport ? SearchMode.SoundCloud : SearchMode.YouTube)
                    .ConfigureAwait(false);
                if (player.State is PlayerState.Destroyed or PlayerState.NotConnected)
                    return;
                await Enqueue(guild.Id, user, lavaTrack3, Platform.Spotify).ConfigureAwait(false);
                if (player.State != PlayerState.Playing)
                {
                    await player.PlayAsync(lavaTrack3).ConfigureAwait(false);
                    await player.SetVolumeAsync(await GetVolume(guild.Id).ConfigureAwait(false) / 100.0F)
                        .ConfigureAwait(false);
                    await ModifySettingsInternalAsync(guild.Id,
                        (musicPlayerSettings, _) => musicPlayerSettings.MusicChannelId = chan.Id,
                        chan.Id).ConfigureAwait(false);
                }

                break;
            default:
                await chan.SendErrorAsync("Seems like that isn't supported at the moment!", config)
                    .ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Moves a song in the queue.
    /// </summary>
    /// <param name="guildId">The guild id to move the song in</param>
    /// <param name="index">The index of the song to move</param>
    /// <param name="newIndex">The new index of the song</param>
    /// <returns></returns>
    public Task<bool> MoveSong(ulong guildId, int index, int newIndex)
    {
        var queue = Queues.GetOrAdd(guildId, new List<LavalinkTrack>());
        try
        {
            _ = queue.ElementAt(--index);
        }
        catch
        {
            return Task.FromResult(false);
        }

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (queue[--index] is null)
            return Task.FromResult(false);
        queue.Move(queue[index], --newIndex);
        return Task.FromResult(true);
    }

    private LavalinkTrack GetCurrentTrack(LavalinkPlayer player, IGuild guild)
    {
        var queue = GetQueue(guild.Id);
        return queue.Find(x => x.Identifier == player.CurrentTrack.Identifier);
    }

    /// <summary>
    /// Attempts to add songs to the queue based on the current song.
    /// </summary>
    /// <param name="guildId">The guild id to add songs to</param>
    public async Task AutoPlay(ulong guildId)
    {
        var guild = this.client.GetGuild(guildId) as IGuild;
        var setting = await GetSettingsInternalAsync(guild.Id);
        var musicChannel = await guild.GetTextChannelAsync(setting.MusicChannelId.Value);
        if (string.IsNullOrWhiteSpace(creds.GoogleApiKey) && string.IsNullOrWhiteSpace(creds.SpotifyClientId))
        {
            await musicChannel.SendErrorAsync(
                "Autoplay relies on either the google or spotify api. Please add a google or spotify api key to the credentials file to use autoplay.",
                config);
            return;
        }

        var spotifyClient = await GetSpotifyClient();
        var lastSong = GetQueue(guild.Id).LastOrDefault();
        if (lastSong is null)
            return;
        var result = await spotifyClient.Search.Item(new SearchRequest(SearchRequest.Types.Track, lastSong.Title));
        if (creds.SpotifyClientId.IsNullOrWhiteSpace() || !result.Tracks.Items.Any())
        {
            if (!lastSong.SourceName.Contains("youtube"))
                return;
            var recommendById = await googleApi.GetVideoLinksByKeywordAsync(
                lastSong.Title.Replace(" ", "+"));
            foreach (var i in recommendById)
            {
                var track = await lavaNode.LoadTracksAsync($"https://www.youtube.com/watch?v={i.Id.VideoId}");
                if (!track.Tracks.Any())
                    continue;
                await Enqueue(guild.Id, this.client.CurrentUser, track.Tracks.FirstOrDefault());
            }

            return;
        }

        var song = result.Tracks.Items.FirstOrDefault();
        var recommendations = await spotifyClient.Browse.GetRecommendations(new RecommendationsRequest
        {
            Limit = 5,
            SeedTracks =
            {
                song.Id
            }
        });
        if (!recommendations.Tracks.Any())
        {
            if (musicChannel is null) return;
            await musicChannel.SendErrorAsync(
                "Unfortunately autoplay could not find any recommendations for your current track. Please queue something else.",
                config);
            return;
        }

        foreach (var i in recommendations.Tracks.Take(setting.AutoPlay))
        {
            var track = await lavaNode.GetTracksAsync($"{i.Artists.FirstOrDefault().Name} {i.Name}",
                SearchMode.YouTube);
            if (!track.Any())
                continue;
            await Enqueue(guild.Id, this.client.CurrentUser, track.FirstOrDefault());
        }
    }

    /// <summary>
    /// Removes a song from the queue.
    /// </summary>
    /// <param name="guild">The guild to remove the song from</param>
    /// <param name="trackNum">The track number to remove</param>
    /// <returns></returns>
    public async Task<bool> RemoveSong(IGuild guild, int trackNum)
    {
        var queue = GetQueue(guild.Id);
        if (queue.Count == 0)
            return false;
        var player = lavaNode.GetPlayer(guild.Id);
        var toRemove = queue?.ElementAt(trackNum - 1);
        var curTrack = GetCurrentTrack(player, guild);
        if (toRemove is null)
            return false;
        var toReplace = queue?.ElementAt(queue.IndexOf(curTrack) + 1);
        if (curTrack == toRemove && toReplace is not null)
            await player.PlayAsync(toReplace).ConfigureAwait(false);
        else if (curTrack == toRemove && toReplace is null)
            await player.StopAsync();
        Queues[guild.Id].Remove(queue.ElementAt(trackNum - 1));
        return true;
    }

    /// <summary>
    /// Gets the current queue.
    /// </summary>
    /// <param name="guildid">The guild id to get the queue for</param>
    /// <returns></returns>
    public List<LavalinkTrack?> GetQueue(ulong guildid) =>
        !Queues.Select(x => x.Key).Contains(guildid)
            ? new List<LavalinkTrack>
            {
                Capacity = 0
            }
            : Queues.FirstOrDefault(x => x.Key == guildid).Value;

    private async Task<SpotifyClient> GetSpotifyClient()
    {
        var spotifyClientConfig = SpotifyClientConfig.CreateDefault();
        var request =
            new ClientCredentialsRequest(creds.SpotifyClientId, creds.SpotifyClientSecret);
        var response = await new OAuthClient(spotifyClientConfig).RequestToken(request).ConfigureAwait(false);
        return new SpotifyClient(spotifyClientConfig.WithToken(response.AccessToken));
    }

    /// <summary>
    /// Skips a song in the queue.
    /// </summary>
    /// <param name="guild">The guild to skip the song in</param>
    /// <param name="chan">The channel to send messages to</param>
    /// <param name="player">The player to skip the song in</param>
    /// <param name="ctx">The interaction context (if executed from a slash command, otherwise null)</param>
    /// <param name="num">The number of songs to skip</param>
    public async Task Skip(IGuild guild, ITextChannel? chan, LavalinkPlayer player, IInteractionContext? ctx = null,
        int num = 1)
    {
        var queue = GetQueue(guild.Id);
        if (queue.Count > 0)
        {
            LavalinkTrack nextTrack;
            var currentTrack = queue.Find(x => player.CurrentTrack.Identifier == x.Identifier);
            try
            {
                nextTrack = queue.ElementAt(queue.IndexOf(currentTrack) + num);
            }
            catch
            {
                if (ctx is not null)
                {
                    await ctx.Interaction.SendErrorAsync("This is the last/only track!", config).ConfigureAwait(false);
                    return;
                }

                await chan.SendErrorAsync("This is the last/only track!", config).ConfigureAwait(false);
                return;
            }

            if ((await GetSettingsInternalAsync(guild.Id).ConfigureAwait(false)).PlayerRepeat == PlayerRepeatType.Track)
            {
                await player.PlayAsync(currentTrack).ConfigureAwait(false);
                if (ctx is not null)
                {
                    await ctx.Interaction.SendConfirmAsync(
                        "Because of the repeat type I am replaying the current song!").ConfigureAwait(false);
                }

                return;
            }

            await player.PlayAsync(nextTrack).ConfigureAwait(false);
            if (ctx is not null)
                await ctx.Interaction.SendConfirmAsync("Playing the next track.").ConfigureAwait(false);
        }
    }

    private async Task HandleDisconnect(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        var player = lavaNode.GetPlayer(before.VoiceChannel?.Guild.Id ?? after.VoiceChannel.Guild.Id);
        if (before.VoiceChannel is null || player is null) return;
        if (player.VoiceChannelId != before.VoiceChannel.Id)
            return;
        if ((await GetSettingsInternalAsync(before.VoiceChannel.Guild.Id)).AutoDisconnect is AutoDisconnect.Either
            or AutoDisconnect.Voice)
        {
            if (before.VoiceChannel.ConnectedUsers.Count - 1 <= 1)
            {
                await player.StopAsync(true);
                await QueueClear(after.VoiceChannel.Guild.Id);
            }
        }
    }

    /// <summary>
    /// Shuffles the current queue.
    /// </summary>
    /// <param name="guild">The guild to shuffle the queue for</param>
    public void Shuffle(IGuild guild) =>
        Queues[guild.Id] = Queues[guild.Id].Shuffle().ToList();

    /// <summary>
    /// Clears the current queue.
    /// </summary>
    /// <param name="guildid">The guild id to clear the queue for</param>
    /// <returns></returns>
    public Task QueueClear(ulong guildid)
    {
        if (!Queues.TryGetValue(guildid, out var queue)) return Task.CompletedTask;
        queue.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current volume.
    /// </summary>
    /// <param name="guildid">The guild id to get the volume for</param>
    /// <returns></returns>
    public async Task<int> GetVolume(ulong guildid) =>
        (await GetSettingsInternalAsync(guildid).ConfigureAwait(false)).Volume;

    /// <summary>
    /// Gets the default playlist for a user.
    /// </summary>
    /// <param name="user">The user to get the default playlist for</param>
    /// <returns></returns>
    public async Task<MusicPlaylist?> GetDefaultPlaylist(IUser user)
    {
        await using var uow = db.GetDbContext();
        return await uow.MusicPlaylists.GetDefaultPlaylist(user.Id);
    }

    /// <summary>
    /// Gets the playlists for a user.
    /// </summary>
    /// <param name="user">The user to get the playlists for</param>
    /// <returns></returns>
    public IEnumerable<MusicPlaylist?> GetPlaylists(IUser user)
    {
        using var uow = db.GetDbContext();
        var a = uow.MusicPlaylists.GetPlaylistsByUser(user.Id);
        return a.ToList();
    }

    /// <summary>
    /// Gets the music player settings for a guild.
    /// </summary>
    /// <param name="guildId">The guild id to get the settings for</param>
    /// <returns>A <see cref="MusicPlayerSettings"/> object</returns>
    public async Task<MusicPlayerSettings> GetSettingsInternalAsync(ulong guildId)
    {
        if (this.settings.TryGetValue(guildId, out var musicPlayerSettings))
            return musicPlayerSettings;

        await using var uow = db.GetDbContext();
        var toReturn = this.settings[guildId] =
            await uow.MusicPlayerSettings.ForGuildAsync(guildId).ConfigureAwait(false);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return toReturn;
    }

    /// <summary>
    /// Modifies the music player settings for a guild.
    /// </summary>
    /// <param name="guildId">The guild id to modify the settings for</param>
    /// <param name="action">The action to perform on the settings</param>
    /// <param name="state">The state to pass to the action</param>
    /// <typeparam name="TState"></typeparam>
    public async Task ModifySettingsInternalAsync<TState>(
        ulong guildId,
        Action<MusicPlayerSettings, TState> action,
        TState state)
    {
        await using var uow = db.GetDbContext();
        var ms = await uow.MusicPlayerSettings.ForGuildAsync(guildId).ConfigureAwait(false);
        action(ms, state);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        settings[guildId] = ms;
    }
}