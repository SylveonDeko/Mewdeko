using System.Threading;
using Lavalink4NET;
using Lavalink4NET.Players;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Music.CustomPlayer;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.Options;

namespace Mewdeko.Modules.Music.Services;

/// <summary>
///     Service that recovers music players after bot restarts.
/// </summary>
public class MusicPlayerRecoveryService : INService, IReadyExecutor
{
    private readonly IAudioService audioService;
    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly ILogger<MusicPlayerRecoveryService> logger;
    private readonly GuildOwnershipService ownership;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MusicPlayerRecoveryService" /> class.
    /// </summary>
    /// <param name="cache">The data cache service.</param>
    /// <param name="audioService">The audio service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="strings">The localization service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="ownership">The service deciding which guilds this instance handles.</param>
    public MusicPlayerRecoveryService(
        IDataCache cache,
        IAudioService audioService,
        DiscordShardedClient client,
        IServiceProvider services,
        GeneratedBotStrings strings, ILogger<MusicPlayerRecoveryService> logger, GuildOwnershipService ownership)
    {
        this.ownership = ownership;
        this.cache = cache;
        this.audioService = audioService;
        this.client = client;
        this.strings = strings;
        this.logger = logger;
    }

    /// <summary>
    ///     Executes when the bot is ready, recovering any active music players.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        try
        {
            logger.LogInformation("Starting music player recovery process");
            await RecoverPlayersAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute player recovery");
        }
    }

    /// <summary>
    ///     Recovers all active music players across all guilds.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RecoverPlayersAsync()
    {
        // Process guilds in batches to avoid rate limiting
        var guilds = client.Guilds.Where(x => ownership.IsMine(x.Id)).ToList();
        logger.LogInformation("Checking {Count} guilds for music player recovery", guilds.Count);

        // Process up to 5 guilds concurrently
        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(5);

        foreach (var guild in guilds)
        {
            await semaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await RecoverGuildPlayerAsync(guild);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        logger.LogInformation("Music player recovery completed");
    }

    /// <summary>
    ///     Recovers a music player for a specific guild.
    /// </summary>
    /// <param name="guild">The guild to recover player for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RecoverGuildPlayerAsync(SocketGuild guild)
    {
        try
        {
            // Check if we have a saved state for this guild
            var state = await cache.GetPlayerState(guild.Id);
            if (state == null)
            {
                logger.LogDebug("No saved player state found for guild {GuildId}", guild.Id);
                return;
            }

            // Check if state is too old (more than 15 minutes)
            if (DateTime.UtcNow - state.LastUpdateTime > TimeSpan.FromMinutes(15))
            {
                logger.LogWarning("Skipping recovery for guild {GuildId} - state too old ({LastUpdate})",
                    guild.Id, state.LastUpdateTime);
                await cache.RemovePlayerState(guild.Id);
                return;
            }

            // Check if voice channel still exists
            var voiceChannel = guild.GetVoiceChannel(state.VoiceChannelId);
            if (voiceChannel == null)
            {
                logger.LogWarning("Skipping recovery for guild {GuildId} - voice channel {ChannelId} no longer exists",
                    guild.Id, state.VoiceChannelId);
                await cache.RemovePlayerState(guild.Id);
                return;
            }

            // Get the queue and current track
            var queue = await cache.GetMusicQueue(guild.Id);
            var currentTrack = await cache.GetCurrentTrack(guild.Id);

            if (currentTrack == null || queue.Count == 0)
            {
                logger.LogWarning("Skipping recovery for guild {GuildId} - no current track or empty queue", guild.Id);
                await cache.RemovePlayerState(guild.Id);
                return;
            }

            logger.LogInformation("Recovering player for guild {GuildId}, track: {TrackTitle}, position: {Position}",
                guild.Id, currentTrack.Track.Title, state.CurrentPosition);

            // Get the music channel
            IMessageChannel messageChannel = null;
            try
            {
                var settings = await cache.GetMusicPlayerSettings(guild.Id);
                if (settings?.MusicChannelId != null)
                    messageChannel = guild.GetTextChannel(settings.MusicChannelId.Value);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not get configured music channel for guild {GuildId}", guild.Id);
            }

            // Fallback to first text channel if needed
            if (messageChannel == null)
            {
                messageChannel = guild.TextChannels
                    .FirstOrDefault(c => c.Users.Any(u => u.Id == client.CurrentUser.Id));

                if (messageChannel == null)
                    messageChannel = guild.DefaultChannel ?? guild.TextChannels.FirstOrDefault();

                if (messageChannel == null)
                {
                    logger.LogWarning("Skipping recovery for guild {GuildId} - cannot find valid text channel",
                        guild.Id);
                    return;
                }
            }


            var retrieveOptions = new PlayerRetrieveOptions(PlayerChannelBehavior.Join);

            var player = await audioService.Players
                .RetrieveAsync<MewdekoPlayer, MewdekoPlayerOptions>(guild.Id, voiceChannel.Id, CreatePlayerAsync,
                    new OptionsWrapper<MewdekoPlayerOptions>(new MewdekoPlayerOptions
                    {
                        Channel = voiceChannel
                    }),
                    retrieveOptions)
                .ConfigureAwait(false);

            // Restore volume
            await player.Player.SetVolumeAsync(state.Volume);
            await player.Player.SetGuildVolumeAsync((int)(state.Volume * 100));

            // Set repeat type and autoplay
            await player.Player.SetRepeatTypeAsync(state.RepeatMode);
            await player.Player.SetAutoPlay(state.AutoPlayAmount);

            // Play track
            await player.Player.PlayAsync(currentTrack.Track);


            await player.Player.SeekAsync(state.CurrentPosition);
            logger.LogDebug("Seeking to position {Position} for guild {GuildId}", state.CurrentPosition, guild.Id);

            // Restore pause state if needed
            if (state.IsPaused)
            {
                await player.Player.PauseAsync();
                logger.LogDebug("Restoring paused state for guild {GuildId}", guild.Id);
            }

            // Send notification to channel
            try
            {
                var resumeEmbed = new EmbedBuilder()
                    .WithTitle(strings.MusicResume(guild.Id))
                    .WithDescription(strings.MusicReconnected(guild.Id) +
                                     $"{state.CurrentPosition:hh\\:mm\\:ss} after bot restart.")
                    .WithColor(new Color(30, 215, 96))
                    .WithFooter(strings.MusicTrackFooter(guild.Id, currentTrack.Track.Title))
                    .Build();

                await messageChannel.SendMessageAsync(embed: resumeEmbed);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not send recovery notification to channel in guild {GuildId}", guild.Id);
            }

            // Clean up state as we've recovered it
            await cache.RemovePlayerState(guild.Id);
            logger.LogInformation("Successfully recovered player for guild {GuildId}", guild.Id);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to recover player for guild {GuildId}:{ex}", guild.Id, ex);

            // Try to clean up the failed state
            try
            {
                await cache.RemovePlayerState(guild.Id);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static ValueTask<MewdekoPlayer> CreatePlayerAsync(
        IPlayerProperties<MewdekoPlayer, MewdekoPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        return ValueTask.FromResult(new MewdekoPlayer(properties));
    }
}