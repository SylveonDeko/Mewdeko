using System.Threading;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Filters;
using Lavalink4NET.Players;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Music.CustomPlayer;

namespace Mewdeko.Modules.Music;

/// <summary>
///     Slash commands for applying audio effects to the music player.
/// </summary>
[Group("musicfx", "Audio effects for the music player")]
public class SlashMusicEffects(
    IAudioService service,
    GuildSettingsService guildSettingsService) : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Sets the bass boost level for the current track. The bass boost enhances low frequencies in the audio.
    /// </summary>
    /// <param name="boost">The bass boost level between 0 and 1.</param>
    [SlashCommand("bass", "Sets the bass boost level between 0 and 1")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Bass([Summary("boost", "Bass boost level between 0 and 1")] double boost = 0)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        if (boost is < 0 or > 1)
        {
            await ReplyErrorAsync(Strings.MusicInvalidBassBoost(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var level = (float)boost;
        var equalizer = new Equalizer
        {
            [0] = level * 0.6f,
            [1] = level * 0.67f,
            [2] = level * 0.67f,
            [3] = level * 0.4f,
            [4] = level * 0.4f,
            [5] = level * 0.3f,
            [6] = level * 0.2f
        };

        player.Filters.Equalizer = new EqualizerFilterOptions(equalizer);
        await player.Filters.CommitAsync().ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.MusicBassBoostSet(ctx.Guild.Id, level)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles the nightcore effect for the current track. The nightcore effect increases speed and pitch.
    /// </summary>
    /// <param name="enable">Whether to enable or disable the effect.</param>
    [SlashCommand("nightcore", "Toggles the nightcore effect")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Nightcore([Summary("enable", "Enable or disable the effect")] bool enable = true)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        if (enable)
        {
            player.Filters.Timescale = new TimescaleFilterOptions
            {
                Speed = 1.2f, Pitch = 1.2f, Rate = 1.0f
            };
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicNightcoreEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            player.Filters.Timescale = null;
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicNightcoreDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Toggles the vaporwave effect for the current track. The vaporwave effect decreases speed and pitch.
    /// </summary>
    /// <param name="enable">Whether to enable or disable the effect.</param>
    [SlashCommand("vaporwave", "Toggles the vaporwave effect")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Vaporwave([Summary("enable", "Enable or disable the effect")] bool enable = true)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        if (enable)
        {
            player.Filters.Timescale = new TimescaleFilterOptions
            {
                Speed = 0.8f, Pitch = 0.8f, Rate = 1.0f
            };
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicVaporwaveEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            player.Filters.Timescale = null;
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicVaporwaveDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Applies or removes a karaoke filter that attempts to remove vocals from the track.
    /// </summary>
    /// <param name="enable">Whether to enable or disable the karaoke filter. Defaults to true.</param>
    [SlashCommand("karaoke", "Toggles the karaoke vocal removal filter")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Karaoke([Summary("enable", "Enable or disable the effect")] bool enable = true)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        if (enable)
        {
            player.Filters.Karaoke = new KaraokeFilterOptions
            {
                Level = 1.0f, MonoLevel = 1.0f, FilterBand = 220.0f, FilterWidth = 100.0f
            };
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicKaraokeEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            player.Filters.Karaoke = null;
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicKaraokeDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Applies or removes a tremolo filter that creates a wavering effect in volume.
    /// </summary>
    /// <param name="enable">Whether to enable or disable the tremolo filter. Defaults to true.</param>
    [SlashCommand("tremolo", "Toggles the tremolo volume wavering effect")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Tremolo([Summary("enable", "Enable or disable the effect")] bool enable = true)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        if (enable)
        {
            player.Filters.Tremolo = new TremoloFilterOptions
            {
                Frequency = 2.0f, Depth = 0.5f
            };
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicTremoloEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            player.Filters.Tremolo = null;
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicTremoloDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Applies or removes a vibrato filter that creates a wavering effect in pitch.
    /// </summary>
    /// <param name="enable">Whether to enable or disable the vibrato filter. Defaults to true.</param>
    [SlashCommand("vibrato", "Toggles the vibrato pitch wavering effect")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Vibrato([Summary("enable", "Enable or disable the effect")] bool enable = true)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        if (enable)
        {
            player.Filters.Vibrato = new VibratoFilterOptions
            {
                Frequency = 2.0f, Depth = 0.5f
            };
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicVibratoEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            player.Filters.Vibrato = null;
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicVibratoDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Applies an 8D audio effect that rotates the audio around the listener.
    /// </summary>
    /// <param name="enable">Whether to enable or disable the rotation effect. Defaults to true.</param>
    [SlashCommand("eight-d", "Toggles the 8D rotating audio effect")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task EightD([Summary("enable", "Enable or disable the effect")] bool enable = true)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        if (enable)
        {
            player.Filters.Rotation = new RotationFilterOptions
            {
                Frequency = 0.2f
            };
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicEightdEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            player.Filters.Rotation = null;
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicEightdDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Applies a mild distortion effect to the audio.
    /// </summary>
    /// <param name="enable">Whether to enable or disable the distortion effect. Defaults to true.</param>
    [SlashCommand("distortion", "Toggles the distortion effect")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Distortion([Summary("enable", "Enable or disable the effect")] bool enable = true)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        if (enable)
        {
            player.Filters.Distortion = new DistortionFilterOptions
            {
                SinOffset = 0.0f,
                SinScale = 1.0f,
                CosOffset = 0.0f,
                CosScale = 1.0f,
                TanOffset = 0.0f,
                TanScale = 1.0f,
                Offset = 0.0f,
                Scale = 0.5f
            };
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicDistortionEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            player.Filters.Distortion = null;
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicDistortionDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Applies a stereo widening effect to enhance the spatial soundstage.
    /// </summary>
    /// <param name="enable">Whether to enable or disable the stereo widening effect. Defaults to true.</param>
    [SlashCommand("stereo-widen", "Toggles the stereo widening effect")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task StereoWiden([Summary("enable", "Enable or disable the effect")] bool enable = true)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        if (enable)
        {
            player.Filters.ChannelMix = new ChannelMixFilterOptions
            {
                LeftToLeft = 1.0f, LeftToRight = 0.5f, RightToLeft = 0.5f, RightToRight = 1.0f
            };
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicStereoWidenEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            player.Filters.ChannelMix = null;
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicStereoWidenDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes all active audio filters from the current track.
    /// </summary>
    [SlashCommand("reset", "Removes all active audio filters")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ResetFilters()
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        player.Filters.Clear();
        await player.Filters.CommitAsync().ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicFiltersReset(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays all currently active audio filters, or indicates that the audio is unmodified.
    /// </summary>
    [SlashCommand("active", "Displays all currently active audio filters")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ActiveFilters()
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var activeFilters = new List<string>();

        if (player.Filters.Equalizer != null) activeFilters.Add("Bass Boost");
        if (player.Filters.Timescale != null)
        {
            var speed = player.Filters.Timescale.Speed;
            switch (speed)
            {
                case > 1.0f:
                    activeFilters.Add("Nightcore");
                    break;
                case < 1.0f:
                    activeFilters.Add("Vaporwave");
                    break;
            }
        }

        if (player.Filters.Karaoke != null) activeFilters.Add("Karaoke");
        if (player.Filters.Tremolo != null) activeFilters.Add("Tremolo");
        if (player.Filters.Vibrato != null) activeFilters.Add("Vibrato");
        if (player.Filters.Rotation != null) activeFilters.Add("8D Audio");
        if (player.Filters.Distortion != null) activeFilters.Add("Distortion");
        if (player.Filters.ChannelMix != null) activeFilters.Add("Stereo Widen");

        if (activeFilters.Count == 0)
        {
            await ReplyConfirmAsync(Strings.MusicNoActiveFilters(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.MusicActiveFilters(ctx.Guild.Id, string.Join(", ", activeFilters)))
                .ConfigureAwait(false);
        }
    }

    private async Task SendPlayerErrorAsync(string result)
    {
        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
            ], Mewdeko.ErrorColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(result));

        if (ctx.Interaction.HasResponded)
        {
            await FollowupAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await RespondAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
            allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    private async ValueTask<(MewdekoPlayer, string?)> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        try
        {
            var channelBehavior = connectToVoiceChannel
                ? PlayerChannelBehavior.Join
                : PlayerChannelBehavior.None;

            var retrieveOptions = new PlayerRetrieveOptions(channelBehavior);

            var options = new MewdekoPlayerOptions
            {
                Channel = ctx.Channel as ITextChannel
            };

            var result = await service.Players
                .RetrieveAsync<MewdekoPlayer, MewdekoPlayerOptions>(Context, CreatePlayerAsync, options,
                    retrieveOptions)
                .ConfigureAwait(false);

            if (result.IsSuccess && result.Player is not null)
            {
                await result.Player.SetVolumeAsync(await result.Player.GetVolume() / 100f).ConfigureAwait(false);
                return (result.Player, null);
            }

            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => Strings.MusicNotInChannel(ctx.Guild.Id),
                PlayerRetrieveStatus.BotNotConnected => Strings.MusicBotNotConnect(ctx.Guild.Id,
                    await guildSettingsService.GetPrefix(ctx.Guild)),
                PlayerRetrieveStatus.VoiceChannelMismatch => Strings.MusicVoiceChannelMismatch(ctx.Guild.Id),
                PlayerRetrieveStatus.Success => null,
                PlayerRetrieveStatus.UserInSameVoiceChannel => null,
                PlayerRetrieveStatus.PreconditionFailed => null,
                _ => throw new ArgumentOutOfRangeException()
            };
            return (null, errorMessage);
        }
        catch (TimeoutException)
        {
            return (null, Strings.MusicLavalinkDisconnected(ctx.Guild.Id));
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