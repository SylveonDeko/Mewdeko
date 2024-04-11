using System.Threading;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Payloads.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Modules.Music.CustomPlayer;

/// <summary>
/// Custom LavaLink player to be able to handle events and such, as well as auto play.
/// </summary>
public sealed class MewdekoPlayer : LavalinkPlayer
{
    private IDataCache cache;
    private IMessageChannel channel;
    private DiscordSocketClient client;
    private DbService dbService;

    /// <summary>
    /// Initializes a new instance of <see cref="MewdekoPlayer"/>.
    /// </summary>
    /// <param name="properties">The player properties.</param>
    public MewdekoPlayer(IPlayerProperties<MewdekoPlayer, MewdekoPlayerOptions> properties) : base(properties)
    {
        channel = properties.Options.Value.Channel;
        client = properties.ServiceProvider.GetRequiredService<DiscordSocketClient>();
        dbService = properties.ServiceProvider.GetRequiredService<DbService>();
        cache = properties.ServiceProvider.GetRequiredService<IDataCache>();
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
        var musicChannel = await GetMusicChannel(base.GuildId);
        var queue = await cache.GetMusicQueue(base.GuildId);
        var currentTrack = queue.IndexOf(item.Track);
        var nextTrack = queue.ElementAtOrDefault(currentTrack + 1);
        switch (reason)
        {
            case TrackEndReason.Finished:
                var repeatType = await GetRepeatType(base.GuildId);
                switch (repeatType)
                {
                    case PlayerRepeatType.None:

                        if (nextTrack is null)
                        {
                            await musicChannel.SendMessageAsync("Queue is empty. Stopping.");
                            await base.StopAsync(token);
                        }
                        else
                        {
                            await base.PlayAsync(nextTrack, cancellationToken: token);
                        }

                        break;
                    case PlayerRepeatType.Track:
                        await base.PlayAsync(item.Track, cancellationToken: token);
                        break;
                    case PlayerRepeatType.Queue:
                        if (nextTrack is null)
                        {
                            await base.PlayAsync(queue[0], cancellationToken: token);
                        }
                        else
                        {
                            await base.PlayAsync(nextTrack, cancellationToken: token);
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
        CancellationToken cancellationToken = new CancellationToken())
    {
        var musicChannel = await GetMusicChannel(base.GuildId);
        var eb = new EmbedBuilder()
            .WithDescription($"Playing [{track.Track.Title}]({track.Track.Uri}) by {track.Track.Author}")
            .WithThumbnailUrl(track.Track.ArtworkUri?.ToString())
            .WithOkColor()
            .Build();

        await musicChannel.SendMessageAsync(embed: eb);
    }

    private async Task<IMessageChannel> GetMusicChannel(ulong guildId)
    {
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
    /// <param name="guildId">The guild id to set the channel for.</param>
    public async Task SetMusicChannelAsync(ulong channelId, ulong guildId)
    {
        await using var uow = dbService.GetDbContext();
        var settings = await uow.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == base.GuildId);
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
    /// Gets the repeat type for the player.
    /// </summary>
    /// <param name="guildId">The guild id to get the repeat type for.</param>
    /// <returns>A <see cref="PlayerRepeatType"/> for the guild.</returns>
    private async Task<PlayerRepeatType> GetRepeatType(ulong guildId)
    {
        await using var uow = dbService.GetDbContext();
        var settings = await uow.MusicPlayerSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        return settings?.PlayerRepeat ?? PlayerRepeatType.Queue;
    }

    /// <summary>
    /// Sets the repeat type for the player.
    /// </summary>
    /// <param name="repeatType">The repeat type to set.</param>
    /// <param name="guildId">The guild id to set the repeat type for.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetRepeatTypeAsync(PlayerRepeatType repeatType, ulong guildId)
    {
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
}