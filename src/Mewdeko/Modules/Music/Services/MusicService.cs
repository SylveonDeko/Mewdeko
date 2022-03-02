using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Modules.Music.Extensions;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SpotifyAPI.Web;
using System.Diagnostics;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;
using Victoria.Responses.Search;

#nullable enable

namespace Mewdeko.Modules.Music.Services;

public class MusicService : INService
{
    private readonly DbService _db;
    private readonly LavaNode _lavaNode;
    private readonly ConcurrentDictionary<ulong, List<AdvancedLavaTrack>> _queues;
    private readonly IBotCredentials _creds;
    private readonly ConcurrentDictionary<ulong, MusicPlayerSettings> _settings;


    public MusicService(LavaNode lava, DbService db, DiscordSocketClient client,
        IBotCredentials creds)
    {
        _db = db;
        this._creds = creds;
        _lavaNode = lava;
        _lavaNode.OnTrackEnded += TrackEnded;
        _settings = new ConcurrentDictionary<ulong, MusicPlayerSettings>();
        _queues = new ConcurrentDictionary<ulong, List<AdvancedLavaTrack>>();
        _lavaNode.OnTrackStarted += TrackStarted;
        client.UserVoiceStateUpdated += HandleDisconnect;
    }

    private async Task<SpotifyClient> GetSpotifyClient()
    {
        var config = SpotifyClientConfig.CreateDefault();
        var request =
            new ClientCredentialsRequest(_creds.SpotifyClientId, _creds.SpotifyClientSecret);
        var response = await new OAuthClient(config).RequestToken(request);
        return new SpotifyClient(config.WithToken(response.AccessToken));
    }
    public Task Enqueue(ulong guildId, IUser user, LavaTrack? lavaTrack,
        Platform queuedPlatform = Platform.Youtube)
    {
        var queue = _queues.GetOrAdd(guildId, new List<AdvancedLavaTrack>());
        queue.Add(new AdvancedLavaTrack(lavaTrack, user, queuedPlatform));
        return Task.CompletedTask;
    }

    public Task Enqueue(ulong guildId, IUser user, LavaTrack[] lavaTracks,
        Platform queuedPlatform = Platform.Youtube)
    {
        var queue = _queues.GetOrAdd(guildId, new List<AdvancedLavaTrack>());
        queue.AddRange(lavaTracks.Select(x => new AdvancedLavaTrack(x, user, queuedPlatform)));
        return Task.CompletedTask;
    }

    public void Shuffle(IGuild guild) => 
        _queues[guild.Id] = _queues[guild.Id].Shuffle().ToList();

    public Task QueueClear(ulong guildid)
    {
        var toremove = _queues.GetOrAdd(guildid, new List<AdvancedLavaTrack>());
        toremove.Clear();
        return Task.CompletedTask;
    }

    public AdvancedLavaTrack GetCurrentTrack(LavaPlayer player, IGuild guild)
    {
        var queue = GetQueue(guild.Id);
        return queue.FirstOrDefault(x => x.Hash == player.Track.Hash);
    }

    public async Task SpotifyQueue(IGuild guild, IUser user, ITextChannel? chan, LavaPlayer player, string? uri)
    {
        Debug.Assert(uri != null, nameof(uri) + " != null");
        var spotifyUrl = new Uri(uri);
        switch (spotifyUrl.Segments[1])
        {
            case "playlist/":
                if (_creds.SpotifyClientId == "")
                {
                    await chan.SendErrorAsync(
                        "Looks like the owner of this bot hasnt added the spotify Id and CLient Secret to their credentials. Spotify queueing wont work without this.");
                    return;
                }
                var result = await (await GetSpotifyClient()).Playlists.Get(spotifyUrl.Segments[2]);
                if (result.Tracks != null && result.Tracks.Items!.Any())
                {
                    var items = result.Tracks.Items;
                    var eb = new EmbedBuilder()
                        .WithAuthor("Spotify Playlist",
                            "https://assets.stickpng.com/images/5ece5029123d6d0004ce5f8b.png")
                        .WithOkColor()
                        .WithDescription($"Trying to queue {items!.Count} tracks from {result.Name}...")
                        .WithThumbnailUrl(result.Images?.FirstOrDefault()?.Url);
                    var msg = await chan!.SendMessageAsync(embed: eb.Build());
                    var addedcount = 0;
                    foreach (var track in items.Select(i => i.Track as FullTrack))
                    {
                        var lavaTrack = await _lavaNode.SearchAsync(SearchType.YouTubeMusic,
                            $"{track?.Name} {track?.Artists.FirstOrDefault()?.Name}");
                        if (lavaTrack.Status is SearchStatus.NoMatches) continue;
                        await Enqueue(guild.Id, user, lavaTrack.Tracks.FirstOrDefault(),
                            Platform.Spotify);
                        if (player.PlayerState != PlayerState.Playing)
                        {
                            await player.PlayAsync(x => x.Track = lavaTrack.Tracks.FirstOrDefault());
                            await player.UpdateVolumeAsync(Convert.ToUInt16(GetVolume(guild.Id)));
                            await ModifySettingsInternalAsync(guild.Id,
                                (settings, _) => settings.MusicChannelId = chan.Id, chan.Id);
                        }

                        addedcount++;
                    }

                    if (addedcount == 0)
                    {
                        eb.WithErrorColor()
                            .WithDescription(
                                $"Seems like I couldn't load any tracks from {result.Name}... Perhaps its private?");
                        await msg.ModifyAsync(x => x.Embed = eb.Build());
                    }

                    eb.WithDescription($"Successfully queued {addedcount} tracks!");
                    await msg.ModifyAsync(x => x.Embed = eb.Build());
                }

                break;
            case "album/":
                if (_creds.SpotifyClientId == "")
                {
                    await chan.SendErrorAsync(
                        "Looks like the owner of this bot hasnt added the spotify Id and CLient Secret to their credentials. Spotify queueing wont work without this.");
                    return;
                }
                var result1 = await (await GetSpotifyClient()).Albums.Get(spotifyUrl.Segments[2]);
#pragma warning disable CS8629 // Nullable value type may be null.
                if ((bool)result1.Tracks.Items?.Any())
#pragma warning restore CS8629 // Nullable value type may be null.
                {
                    var items = result1.Tracks.Items;
                    var eb = new EmbedBuilder()
                        .WithAuthor("Spotify Album", "https://assets.stickpng.com/images/5ece5029123d6d0004ce5f8b.png")
                        .WithOkColor()
                        .WithDescription($"Trying to queue {items.Count} tracks from {result1.Name}...")
                        .WithThumbnailUrl(result1.Images.FirstOrDefault()?.Url);
                    var msg = await chan!.SendMessageAsync(embed: eb.Build());
                    var addedcount = 0;
                    foreach (var track in items)
                    {
                        var lavaTrack = await _lavaNode.SearchAsync(SearchType.YouTubeMusic,
                            $"{track.Name} {track.Artists.FirstOrDefault()?.Name}");
                        if (lavaTrack.Status is SearchStatus.NoMatches) continue;
                        await Enqueue(guild.Id, user, lavaTrack.Tracks.FirstOrDefault(),
                            Platform.Spotify);
                        if (player.PlayerState != PlayerState.Playing)
                        {
                            await player.PlayAsync(x => x.Track = lavaTrack.Tracks.FirstOrDefault());
                            await player.UpdateVolumeAsync(Convert.ToUInt16(GetVolume(guild.Id)));
                            await ModifySettingsInternalAsync(guild.Id,
                                (settings, _) => settings.MusicChannelId = chan.Id, chan.Id);
                        }

                        addedcount++;
                    }

                    if (addedcount == 0)
                    {
                        eb.WithErrorColor()
                            .WithDescription(
                                $"Seems like I couldn't load any tracks from {result1.Name}... Perhaps the songs weren't found or are exclusive?");
                        await msg.ModifyAsync(x => x.Embed = eb.Build());
                    }

                    eb
                        .WithDescription($"Successfully queued {addedcount} tracks!")
                        .WithTitle(result1.Name);
                    await msg.ModifyAsync(x => x.Embed = eb.Build());
                }

                break;

            case "track/":
                if (_creds.SpotifyClientId == "")
                {
                    await chan.SendErrorAsync(
                        "Looks like the owner of this bot hasnt added the spotify Id and CLient Secret to their credentials. Spotify queueing wont work without this.");
                    return;
                }
                var result3 = await (await GetSpotifyClient()).Tracks.Get(spotifyUrl.Segments[2]);
                if (result3.Name is null)
                {
                    await chan.SendErrorAsync(
                        "Seems like i can't find or play this. Please try with a different link!");
                    return;
                }

                var lavaTrack3 = await _lavaNode.SearchAsync(SearchType.YouTubeMusic,
                    $"{result3.Name} {result3.Artists.FirstOrDefault()?.Name}");
                await Enqueue(guild.Id, user, lavaTrack3.Tracks.FirstOrDefault(), Platform.Spotify);
                if (player.PlayerState != PlayerState.Playing)
                {
                    await player.PlayAsync(x => x.Track = lavaTrack3.Tracks.FirstOrDefault());
                    await player.UpdateVolumeAsync(Convert.ToUInt16(GetVolume(guild.Id)));
                    await ModifySettingsInternalAsync(guild.Id,
                        (settings, _) => settings.MusicChannelId = chan.Id, chan.Id);
                }

                break;
            default:
                await chan.SendErrorAsync("Seems like that isn't supported at the moment!");
                break;
        }
    }

    public async Task<bool> RemoveSong(IGuild guild, int trackNum)
    {
        var queue = GetQueue(guild.Id);
        if (!queue.Any())
            return false;
        _lavaNode.TryGetPlayer(guild, out var player);
        var toRemove = queue.ElementAt(trackNum - 1);
        var curTrack = GetCurrentTrack(player, guild);
        if (toRemove.Source is null)
            return false;
        var toReplace = queue.ElementAt(queue.IndexOf(curTrack) + 1);
        if (curTrack == toRemove && toReplace.Source != null)
            await player.PlayAsync(queue.ElementAt(queue.IndexOf(curTrack) + 1));
        else
        {
            await player.StopAsync();
        }
        _queues[guild.Id].Remove(queue.ElementAt(trackNum - 1));
        return true;
    }

    public List<AdvancedLavaTrack> GetQueue(ulong guildid) =>
        !_queues.Select(x => x.Key).Contains(guildid)
            ? new List<AdvancedLavaTrack>
            {
                Capacity = 0
            }
            : _queues.FirstOrDefault(x => x.Key == guildid).Value;

    private async Task HandleDisconnect(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (before.VoiceChannel is not null && _lavaNode.TryGetPlayer(before.VoiceChannel.Guild, out _))
            if (before.VoiceChannel.Users.Count == 1 &&
                GetSettingsInternalAsync(before.VoiceChannel.Guild.Id).Result.AutoDisconnect is AutoDisconnect.Either
                    or AutoDisconnect.Voice)
                try
                {
                    await _lavaNode.LeaveAsync(before.VoiceChannel);
                    await QueueClear(before.VoiceChannel.Guild.Id);
                }
                catch
                {
                    // ignored
                }
    }

    private async Task TrackStarted(TrackStartEventArgs args)
    {
        var queue = GetQueue(args.Player.VoiceChannel.GuildId);
        var track = queue.FirstOrDefault(x => x.Url == args.Track.Url);
        var nextTrack = queue.ElementAt(queue.IndexOf(track) + 1);
        var resultMusicChannelId = GetSettingsInternalAsync(args.Player.VoiceChannel.GuildId).Result.MusicChannelId;
        if (resultMusicChannelId != null)
        {
            var channel = await args.Player.VoiceChannel.Guild.GetTextChannelAsync(
                resultMusicChannelId.Value);
            if (channel is not null)
            {
                if (track.Source != null)
                {
                    var eb = new EmbedBuilder()
                             .WithOkColor()
                             .WithDescription($"Now playing {track.Title} by {track.Author}")
                             .WithTitle($"Track #{queue.IndexOf(track)+1}")
                             .WithFooter(
                                 $"{track.Duration:hh\\:mm\\:ss} | {track.QueueUser} | {track.QueuedPlatform} | {queue.Count} tracks in queue")
                             .WithThumbnailUrl(track.FetchArtworkAsync().Result);
                    if (nextTrack.Source is not null) eb.AddField("Up Next", $"{nextTrack.Title} by {nextTrack.Author}");

                    await channel.SendMessageAsync(embed: eb.Build());
                }
            }
        }
    }

    private async Task TrackEnded(TrackEndedEventArgs args)
    {
        var queue = GetQueue(args.Player.VoiceChannel.GuildId);
        if (queue.Any())
        {
            var gid = args.Player.VoiceChannel.GuildId;
            var msettings = await GetSettingsInternalAsync(gid);
            var channel = await args.Player.VoiceChannel.Guild.GetTextChannelAsync(msettings.MusicChannelId!.Value);
            if (args.Reason is TrackEndReason.Replaced or TrackEndReason.Stopped or TrackEndReason.Cleanup) return;
            var currentTrack = queue.FirstOrDefault(x => args.Track.Hash == x.Hash);
            if (msettings.PlayerRepeat == PlayerRepeatType.Track)
            {
                await args.Player.PlayAsync(currentTrack);
                return;
            }

            var nextTrack = queue.ElementAt(queue.IndexOf(currentTrack) + 1);
            if (nextTrack.Source is null && channel != null)
            {
                if (msettings.PlayerRepeat == PlayerRepeatType.Queue)
                {
                    await args.Player.PlayAsync(GetQueue(gid).FirstOrDefault());
                    return;
                }

                var eb1 = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription("I have reached the end of the queue!");
                await channel.SendMessageAsync(embed: eb1.Build());
                if (GetSettingsInternalAsync(args.Player.VoiceChannel.Guild.Id).Result.AutoDisconnect is
                    AutoDisconnect.Either or AutoDisconnect.Queue)
                {
                    await _lavaNode.LeaveAsync(args.Player.VoiceChannel);
                    return;
                }
            }

            await args.Player.PlayAsync(nextTrack);
        }
    }

    public int GetVolume(ulong guildid) => GetSettingsInternalAsync(guildid).Result.Volume;

    public async Task Skip(IGuild guild, ITextChannel? chan, LavaPlayer player, IInteractionContext? ctx = null)
    {
        var queue = GetQueue(guild.Id);
        if (queue.Any())
        {
            var currentTrack = queue.FirstOrDefault(x => player.Track.Hash == x.Hash);
            var nextTrack = queue.ElementAt(queue.IndexOf(currentTrack) + 1);
            if (nextTrack.Source is null)
            {
                if (ctx is not null)
                {
                    await ctx.Interaction.SendErrorAsync("This is the last track!");
                    return;
                }
                await chan.SendErrorAsync("This is the last track!");
                return;
            }

            if (GetSettingsInternalAsync(guild.Id).Result.PlayerRepeat == PlayerRepeatType.Track)
            {
                await player.PlayAsync(currentTrack);
                if (ctx is not null)
                    await ctx.Interaction.SendConfirmAsync(
                        "Because of the repeat type I am replaying the current song!");
                return;
            }

            await player.PlayAsync(nextTrack);
            if (ctx is not null)
                await ctx.Interaction.SendConfirmAsync("Playing the next track.");
        }
    }

    public async Task UpdateDefaultPlaylist(IUser user, MusicPlaylist mpl)
    {
        await using var uow = _db.GetDbContext();
        var def = uow.MusicPlaylists.GetDefaultPlaylist(user.Id);
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
        await uow.SaveChangesAsync();
    }

    public MusicPlaylist GetDefaultPlaylist(IUser user)
    {
        using var uow = _db.GetDbContext();
        return uow.MusicPlaylists.GetDefaultPlaylist(user.Id);
    }
    public IEnumerable<MusicPlaylist> GetPlaylists(IUser user)
    {
        var uow = _db.GetDbContext();
        return uow.MusicPlaylists.GetPlaylistsByUser(user.Id);
    }
    private async Task<MusicPlayerSettings> GetSettingsInternalAsync(ulong guildId)
    {
        if (_settings.TryGetValue(guildId, out var settings))
            return settings;

        await using var uow = _db.GetDbContext();
        var toReturn = _settings[guildId] = await uow.MusicPlayerSettings.ForGuildAsync(guildId);
        await uow.SaveChangesAsync();

        return toReturn;
    }

    public async Task ModifySettingsInternalAsync<TState>(
        ulong guildId,
        Action<MusicPlayerSettings, TState> action,
        TState state)
    {
        await using var uow = _db.GetDbContext();
        var ms = await uow.MusicPlayerSettings.ForGuildAsync(guildId);
        action(ms, state);
        await uow.SaveChangesAsync();
        _settings[guildId] = ms;
    }
}