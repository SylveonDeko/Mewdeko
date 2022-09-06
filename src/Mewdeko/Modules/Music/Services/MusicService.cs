using Lavalink4NET;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Services.Settings;
using SpotifyAPI.Web;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Music.Services;

public class MusicService : INService
{
    public readonly ConcurrentDictionary<ulong, List<LavalinkTrack?>> Queues;
    private readonly ConcurrentDictionary<ulong, MusicPlayerSettings> _settings;
    private readonly DbService _db;
    private readonly LavalinkNode _lavaNode;
    private readonly IBotCredentials _creds;
    private readonly IGoogleApiService _googleApi;
    private readonly DiscordSocketClient _client;
    private readonly BotConfigService _config;

    public MusicService(LavalinkNode lavaNode, IBotCredentials creds, DbService db, EventHandler eventHandler,
        IGoogleApiService googleApi,
        DiscordSocketClient client,
        BotConfigService config)
    {
        _lavaNode = lavaNode;
        _creds = creds;
        _db = db;
        _googleApi = googleApi;
        _client = client;
        _config = config;
        _settings = new ConcurrentDictionary<ulong, MusicPlayerSettings>();
        Queues = new ConcurrentDictionary<ulong, List<LavalinkTrack>>();
        eventHandler.UserVoiceStateUpdated += HandleDisconnect;
    }

    public async Task<string> GetPrettyInfo(LavalinkPlayer player, IGuild guild)
    {
        var currentTrack = player.CurrentTrack;
        var currentContext = currentTrack.Context as AdvancedTrackContext;
        var musicSettings = await GetSettingsInternalAsync(guild.Id).ConfigureAwait(false);
        return
            $"{player.Position.Position:hh\\:mm\\:ss}/{currentTrack.Duration:hh\\:mm\\:ss} | {currentContext.QueueUser} | {currentContext.QueuedPlatform} | Vol: {musicSettings.Volume} | Loop: {musicSettings.PlayerRepeat} | {GetQueue(guild.Id).Count} tracks in queue";
    }
    public async Task UpdateDefaultPlaylist(IUser user, MusicPlaylist mpl)
    {
        await using var uow = _db.GetDbContext();
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

    public async Task SpotifyQueue(
        IGuild guild,
        IUser user,
        ITextChannel? chan,
        LavalinkPlayer player,
        string? uri)
    {
        Debug.Assert(uri != null, $"{nameof(uri)} != null");
        var spotifyUrl = new Uri(uri);
        switch (spotifyUrl.Segments[1])
        {
            case "playlist/":
                if (_creds.SpotifyClientId is null or "")
                {
                    await chan.SendErrorAsync(
                        "Looks like the owner of this bot hasnt added the spotify Id and CLient Secret to their credentials. Spotify queueing wont work without this.").ConfigureAwait(false);
                    return;
                }

                var result = await (await GetSpotifyClient().ConfigureAwait(false)).Playlists.Get(spotifyUrl.Segments[2]).ConfigureAwait(false);
                if (result.Tracks != null && result.Tracks.Items!.Count > 0)
                {
                    var items = result.Tracks.Items;
                    var eb = new EmbedBuilder()
                             .WithAuthor("Spotify Playlist",
                                 "https://assets.stickpng.com/images/5ece5029123d6d0004ce5f8b.png").WithOkColor()
                             .WithDescription($"Trying to queue {items!.Count} tracks from {result.Name}...")
                             .WithThumbnailUrl(result.Images?.FirstOrDefault()?.Url);
                    var msg = await chan!.SendMessageAsync(embed: eb.Build(), 
                        components: _config.Data.ShowInviteButton ? new ComponentBuilder()
                                                                    .WithButton(style: ButtonStyle.Link, 
                                                                        url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands", 
                                                                        label: "Invite Me!", 
                                                                        emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build() : null).ConfigureAwait(false);
                    var addedcount = 0;
                    foreach (var track in items.Select(i => i.Track as FullTrack))
                    {
                        if (player.State is PlayerState.Destroyed or PlayerState.NotConnected)
                            return;
                        var lavaTrack = await _lavaNode.GetTrackAsync(
                            $"{track?.Name} {track?.Artists.FirstOrDefault()?.Name}", SearchMode.YouTube).ConfigureAwait(false);
                        if (lavaTrack is null) continue;
                        await Enqueue(guild.Id, user, lavaTrack, Platform.Spotify).ConfigureAwait(false);
                        if (player.State != PlayerState.Playing && player.State != PlayerState.Destroyed)
                        {
                            await player.PlayAsync(lavaTrack).ConfigureAwait(false);
                            await player.SetVolumeAsync(await GetVolume(guild.Id).ConfigureAwait(false) / 100.0F).ConfigureAwait(false);
                            await ModifySettingsInternalAsync(guild.Id,
                                (settings, _) => settings.MusicChannelId = chan.Id, chan.Id).ConfigureAwait(false);
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
                if (string.IsNullOrEmpty(_creds.SpotifyClientId))
                {
                    await chan.SendErrorAsync(
                        "Looks like the owner of this bot hasnt added the spotify Id and CLient Secret to their credentials. Spotify queueing wont work without this.").ConfigureAwait(false);
                    return;
                }

                var result1 = await (await GetSpotifyClient().ConfigureAwait(false)).Albums.Get(spotifyUrl.Segments[2]).ConfigureAwait(false);
                if (result1.Tracks.Items.Any())
                {
                    var items = result1.Tracks.Items;
                    var eb = new EmbedBuilder()
                             .WithAuthor("Spotify Album",
                                 "https://assets.stickpng.com/images/5ece5029123d6d0004ce5f8b.png").WithOkColor()
                             .WithDescription($"Trying to queue {items.Count} tracks from {result1.Name}...")
                             .WithThumbnailUrl(result1.Images.FirstOrDefault()?.Url);
                    var msg = await chan!.SendMessageAsync(embed: eb.Build(), 
                        components: _config.Data.ShowInviteButton ? new ComponentBuilder()
                                                                    .WithButton(style: ButtonStyle.Link, 
                                                                        url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands", 
                                                                        label: "Invite Me!", 
                                                                        emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build() : null).ConfigureAwait(false);
                    var addedcount = 0;
                    foreach (var track in items)
                    {
                        if (player.State is PlayerState.Destroyed or PlayerState.NotConnected)
                            return;
                        var lavaTrack = await _lavaNode.GetTrackAsync(
                            $"{track.Name} {track.Artists.FirstOrDefault()?.Name}").ConfigureAwait(false);
                        if (lavaTrack is null) continue;
                        await Enqueue(guild.Id, user, lavaTrack, Platform.Spotify).ConfigureAwait(false);
                        if (player.State != PlayerState.Playing)
                        {
                            await player.PlayAsync(lavaTrack).ConfigureAwait(false);
                            await player.SetVolumeAsync(await GetVolume(guild.Id).ConfigureAwait(false) / 100.0F).ConfigureAwait(false);
                            await ModifySettingsInternalAsync(guild.Id,
                                (settings, _) => settings.MusicChannelId = chan.Id, chan.Id).ConfigureAwait(false);
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
                if (string.IsNullOrEmpty(_creds.SpotifyClientId))
                {
                    await chan.SendErrorAsync(
                        "Looks like the owner of this bot hasnt added the spotify Id and CLient Secret to their credentials. Spotify queueing wont work without this.").ConfigureAwait(false);
                    return;
                }

                var result3 = await (await GetSpotifyClient().ConfigureAwait(false)).Tracks.Get(spotifyUrl.Segments[2]).ConfigureAwait(false);
                if (string.IsNullOrEmpty(result3.Name))
                {
                    await chan.SendErrorAsync(
                        "Seems like i can't find or play this. Please try with a different link!").ConfigureAwait(false);
                    return;
                }

                var lavaTrack3 = await _lavaNode.GetTrackAsync(
                    $"{result3.Name} {result3.Artists.FirstOrDefault()?.Name}", SearchMode.YouTube).ConfigureAwait(false);
                if (player.State is PlayerState.Destroyed or PlayerState.NotConnected)
                    return;
                await Enqueue(guild.Id, user, lavaTrack3, Platform.Spotify).ConfigureAwait(false);
                if (player.State != PlayerState.Playing)
                {
                    await player.PlayAsync(lavaTrack3).ConfigureAwait(false);
                    await player.SetVolumeAsync(await GetVolume(guild.Id).ConfigureAwait(false) / 100.0F).ConfigureAwait(false);
                    await ModifySettingsInternalAsync(guild.Id, (settings, _) => settings.MusicChannelId = chan.Id,
                        chan.Id).ConfigureAwait(false);
                }

                break;
            default:
                await chan.SendErrorAsync("Seems like that isn't supported at the moment!").ConfigureAwait(false);
                break;
        }
    }

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
    public LavalinkTrack GetCurrentTrack(LavalinkPlayer player, IGuild guild)
    {
        var queue = GetQueue(guild.Id);
        return queue.Find(x => x.Identifier == player.CurrentTrack.Identifier);
    }

    public async Task AutoPlay(ulong guildId)
    {
        var guild = _client.GetGuild(guildId) as IGuild;
        var setting = await GetSettingsInternalAsync(guild.Id);
        var musicChannel = await guild.GetTextChannelAsync(setting.MusicChannelId.Value);
        if (string.IsNullOrWhiteSpace(_creds.GoogleApiKey) && string.IsNullOrWhiteSpace(_creds.SpotifyClientId))
        {
            await musicChannel.SendErrorAsync(
                "Autoplay relies on either the google or spotify api. Please add a google or spotify api key to the credentials file to use autoplay.");
            return;
        }
        var client = await GetSpotifyClient();
        var lastSong = GetQueue(guild.Id).LastOrDefault();
        if (lastSong is null)
            return;
        var result = await client.Search.Item(new SearchRequest(SearchRequest.Types.Track, lastSong.Title));
        if (_creds.SpotifyClientId.IsNullOrWhiteSpace() || !result.Tracks.Items.Any())
        {
            if (!lastSong.SourceName.Contains("youtube"))
                return;
            var recommendById = await _googleApi.GetVideoLinksByVideoId(lastSong.TrackIdentifier, setting.AutoPlay);
            foreach (var i in recommendById)
            {
                var track = await _lavaNode.LoadTracksAsync($"https://www.youtube.com/watch?v={i.Id.VideoId}");
                if (!track.Tracks.Any())
                    continue;
                await Enqueue(guild.Id, _client.CurrentUser, track.Tracks.FirstOrDefault());
            }
            return;
        }
        var song = result.Tracks.Items.FirstOrDefault();
        var recommendations = await client.Browse.GetRecommendations(new RecommendationsRequest
        {
            Limit = 5, SeedTracks = { song.Id }
        });
        if (!recommendations.Tracks.Any())
        {
            if (musicChannel is null) return;
            await musicChannel.SendErrorAsync("Unfortunately autoplay could not find any recommendations for your current track. Please queue something else.");
            return;
        }
        foreach (var i in recommendations.Tracks.Take(setting.AutoPlay))
        {
            var track = await _lavaNode.GetTracksAsync($"{i.Artists.FirstOrDefault().Name} {i.Name}", SearchMode.YouTube);
            if (!track.Any())
                continue;
            await Enqueue(guild.Id, _client.CurrentUser, track.FirstOrDefault());
        }
    }

    public async Task<bool> RemoveSong(IGuild guild, int trackNum)
    {
        var queue = GetQueue(guild.Id);
        if (queue.Count == 0)
            return false;
        var player = _lavaNode.GetPlayer(guild.Id);
        var toRemove = queue?.ElementAt(trackNum - 1);
        var curTrack = GetCurrentTrack(player, guild);
        if (toRemove is null)
            return false;
        var toReplace = queue?.ElementAt(queue.IndexOf(curTrack) + 1);
        if (curTrack == toRemove && toReplace is not null)
        {
            await player.PlayAsync(toReplace).ConfigureAwait(false);
        }
        else
        {
            await player.StopAsync().ConfigureAwait(false);
        }

        Queues[guild.Id].Remove(queue.ElementAt(trackNum - 1));
        return true;
    }

    public List<LavalinkTrack?> GetQueue(ulong guildid) =>
        !Queues.Select(x => x.Key).Contains(guildid)
            ? new List<LavalinkTrack> { Capacity = 0 }
            : Queues.FirstOrDefault(x => x.Key == guildid).Value;

    private async Task<SpotifyClient> GetSpotifyClient()
    {
        var config = SpotifyClientConfig.CreateDefault();
        var request =
            new ClientCredentialsRequest(_creds.SpotifyClientId, _creds.SpotifyClientSecret);
        var response = await new OAuthClient(config).RequestToken(request).ConfigureAwait(false);
        return new SpotifyClient(config.WithToken(response.AccessToken));
    }

    public async Task Skip(IGuild guild, ITextChannel? chan, LavalinkPlayer player, IInteractionContext? ctx = null, int num = 1)
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
                    await ctx.Interaction.SendErrorAsync("This is the last/only track!").ConfigureAwait(false);
                    return;
                }
                await chan.SendErrorAsync("This is the last/only track!").ConfigureAwait(false);
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

    private Task HandleDisconnect(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        _ = Task.Run(async () =>
        {
            var player = _lavaNode.GetPlayer(before.VoiceChannel?.Guild.Id ?? after.VoiceChannel.Guild.Id);
            if (before.VoiceChannel is not null && player is not null)
            {
                if (player.VoiceChannelId != before.VoiceChannel.Id)
                    return;
                if (before.VoiceChannel.Users.Count == 1
                    && (await GetSettingsInternalAsync(before.VoiceChannel.Guild.Id).ConfigureAwait(false)).AutoDisconnect is AutoDisconnect.Either or AutoDisconnect.Voice)
                {
                    try
                    {
                        await player.StopAsync(true).ConfigureAwait(false);
                        await QueueClear(before.VoiceChannel.Guild.Id).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        });
        return Task.CompletedTask;
    }

    public void Shuffle(IGuild guild) =>
        Queues[guild.Id] = Queues[guild.Id].Shuffle().ToList();

    public Task QueueClear(ulong guildid)
    {
        if (!Queues.TryGetValue(guildid, out var queue)) return Task.CompletedTask;
        queue.Clear();
        return Task.CompletedTask;
    }
    public async Task<int> GetVolume(ulong guildid) => (await GetSettingsInternalAsync(guildid).ConfigureAwait(false)).Volume;

    public async Task<MusicPlaylist> GetDefaultPlaylist(IUser user)
    {
        await using var uow = _db.GetDbContext();
        return await uow.MusicPlaylists.GetDefaultPlaylist(user.Id);
    }
    public IEnumerable<MusicPlaylist?> GetPlaylists(IUser user)
    {
        using var uow = _db.GetDbContext();
        var a = uow.MusicPlaylists.GetPlaylistsByUser(user.Id);
        return a.ToList();
    }
    public async Task<MusicPlayerSettings> GetSettingsInternalAsync(ulong guildId)
    {
        if (_settings.TryGetValue(guildId, out var settings))
            return settings;

        await using var uow = _db.GetDbContext();
        var toReturn = _settings[guildId] = await uow.MusicPlayerSettings.ForGuildAsync(guildId).ConfigureAwait(false);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return toReturn;
    }

    public async Task ModifySettingsInternalAsync<TState>(
        ulong guildId,
        Action<MusicPlayerSettings, TState> action,
        TState state)
    {
        await using var uow = _db.GetDbContext();
        var ms = await uow.MusicPlayerSettings.ForGuildAsync(guildId).ConfigureAwait(false);
        action(ms, state);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _settings[guildId] = ms;
    }
}