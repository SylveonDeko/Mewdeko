using Discord.Commands;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Filters;
using Lavalink4NET.Integrations.ExtraFilters;
using Lavalink4NET.Player;
using Lavalink4NET.Players;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;

namespace Mewdeko.Modules.Music;

public partial class Music
{
    /// <summary>
    ///     Sets various audio effects for the player
    /// </summary>
    public class MusicEffects(
        IAudioService service,
        GuildSettingsService guildSettingsService) : MewdekoSubmodule
    {
        /// <summary>
        ///     Sets the bass boost level for the current track.
        /// </summary>
        /// <param name="boost">The bass boost level between 0 and 1.</param>
        /// <remarks>
        ///     The bass boost command enhances low frequencies in the audio.
        ///     Usage: .bass 0.5
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Bass(float boost = 0)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                return;
            }

            if (boost is < 0 or > 1)
            {
                await ReplyErrorAsync(Strings.MusicInvalidBassBoost(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var equalizer = new Equalizer
            {
                [0] = boost * 0.6f, // 25 Hz
                [1] = boost * 0.67f, // 40 Hz
                [2] = boost * 0.67f, // 63 Hz
                [3] = boost * 0.4f, // 100 Hz
                [4] = boost * 0.4f, // 160 Hz
                [5] = boost * 0.3f, // 250 Hz
                [6] = boost * 0.2f // 400 Hz
            };

            player.Filters.Equalizer = new EqualizerFilterOptions(equalizer);
            await player.Filters.CommitAsync().ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.MusicBassBoostSet(ctx.Guild.Id, boost)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the nightcore effect for the current track.
        /// </summary>
        /// <param name="enable">Whether to enable or disable the effect.</param>
        /// <remarks>
        ///     The nightcore effect increases speed and pitch of the audio.
        ///     Usage: .nightcore or .nightcore false
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Nightcore(bool enable = true)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
        ///     Toggles the vaporwave effect for the current track.
        /// </summary>
        /// <param name="enable">Whether to enable or disable the effect.</param>
        /// <remarks>
        ///     The vaporwave effect decreases speed and pitch of the audio.
        ///     Usage: .vaporwave or .vaporwave false
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Vaporwave(bool enable = true)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
        /// <remarks>
        ///     The karaoke filter uses the following settings:
        ///     - Level: 1.0 (Main karaoke effect level)
        ///     - MonoLevel: 1.0 (Stereo to mono conversion level)
        ///     - FilterBand: 220.0 (Center frequency for vocal removal)
        ///     - FilterWidth: 100.0 (Width of the band to remove)
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Karaoke(bool enable = true)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
        /// <remarks>
        ///     The tremolo filter uses the following settings:
        ///     - Frequency: 2.0 (Speed of the volume variation in Hz)
        ///     - Depth: 0.5 (Intensity of the volume variation from 0 to 1)
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Tremolo(bool enable = true)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
        /// <remarks>
        ///     The vibrato filter uses the following settings:
        ///     - Frequency: 2.0 (Speed of the pitch variation in Hz)
        ///     - Depth: 0.5 (Intensity of the pitch variation from 0 to 1)
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Vibrato(bool enable = true)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
        /// <remarks>
        ///     Creates a spatial audio effect that makes the sound appear to rotate around the listener's head.
        ///     The rotation speed is set to 0.2 rotations per second, creating a smooth, immersive effect.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task EightD(bool enable = true)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
        ///     Applies a distortion effect to the audio.
        /// </summary>
        /// <param name="enable">Whether to enable or disable the distortion effect. Defaults to true.</param>
        /// <remarks>
        ///     Adds mild distortion to the audio that enhances certain genres like rock or electronic music.
        ///     Uses carefully tuned parameters to avoid excessive distortion while still providing a noticeable effect.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Distortion(bool enable = true)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
        /// <remarks>
        ///     Enhances the stereo separation of the audio to create a wider soundstage.
        ///     This effect can make the audio feel more spacious and immersive.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task StereoWiden(bool enable = true)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
        ///     Applies a low pass filter that muffles the audio by cutting high frequencies.
        /// </summary>
        /// <param name="smoothing">
        ///     The smoothing amount between 1 and 100. Higher values cut more treble. 0 disables the filter.
        /// </param>
        /// <remarks>
        ///     Usage: .lowpass 20 or .lowpass 0 to disable
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task LowPass(float smoothing = 20)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                return;
            }

            if (smoothing <= 0)
            {
                player.Filters.LowPass = null;
                await player.Filters.CommitAsync().ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.MusicLowPassDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (smoothing is < 1 or > 100)
            {
                await ReplyErrorAsync(Strings.MusicInvalidLowPass(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            player.Filters.LowPass = new LowPassFilterOptions(smoothing);
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicLowPassEnabled(ctx.Guild.Id, smoothing)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Applies an echo effect to the current track. Requires the echo filter plugin on the Lavalink node.
        /// </summary>
        /// <param name="delay">The echo delay in seconds between 0.1 and 5. 0 disables the effect.</param>
        /// <param name="decay">How much each echo fades, between 0 and 1.</param>
        /// <remarks>
        ///     Usage: .echo 1 0.5 or .echo 0 to disable
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Echo(float delay = 1, float decay = 0.5f)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                return;
            }

            if (delay <= 0)
            {
                player.Filters.TryRemove<EchoFilterOptions>();
                await player.Filters.CommitAsync().ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.MusicEchoDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (delay is < 0.1f or > 5 || decay is < 0 or > 1)
            {
                await ReplyErrorAsync(Strings.MusicInvalidEcho(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            player.Filters.Echo(new EchoFilterOptions(delay, decay));
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicEchoEnabled(ctx.Guild.Id, delay, decay)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles volume normalization, which evens out loud and quiet parts of the audio. Requires the
        ///     normalization filter plugin on the Lavalink node.
        /// </summary>
        /// <param name="enable">Whether to enable or disable normalization. Defaults to true.</param>
        /// <remarks>
        ///     Usage: .normalize or .normalize false
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Normalize(bool enable = true)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                return;
            }

            if (enable)
            {
                player.Filters.SetFilter(new NormalizationFilterOptions(0.75f, true));
                await player.Filters.CommitAsync().ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.MusicNormalizeEnabled(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                player.Filters.TryRemove<NormalizationFilterOptions>();
                await player.Filters.CommitAsync().ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.MusicNormalizeDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes all active audio filters from the current track.
        /// </summary>
        /// <remarks>
        ///     This will disable all active filters including:
        ///     - Bass boost (Equalizer)
        ///     - Nightcore/Vaporwave (Timescale)
        ///     - Karaoke
        ///     - Tremolo
        ///     - Vibrato
        ///     - Low pass, echo and normalization
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ResetFilters()
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                return;
            }

            player.Filters.Clear();
            await player.Filters.CommitAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicFiltersReset(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Displays all currently active audio filters.
        /// </summary>
        /// <remarks>
        ///     Lists all active filters which may include:
        ///     - Bass Boost
        ///     - Nightcore
        ///     - Vaporwave
        ///     - Karaoke
        ///     - Tremolo
        ///     - Vibrato
        ///     - 8D Audio
        ///     - Distortion
        ///     - Stereo Widen
        ///     If no filters are active, it will indicate that the audio is unmodified.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ActiveFilters()
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
            if (player.Filters.LowPass != null) activeFilters.Add("Low Pass");
            if (player.Filters.GetFilter<EchoFilterOptions>() != null) activeFilters.Add("Echo");
            if (player.Filters.GetFilter<NormalizationFilterOptions>() != null) activeFilters.Add("Normalization");

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

                await result.Player.SetVolumeAsync(await result.Player.GetVolume() / 100f).ConfigureAwait(false);

                if (result.IsSuccess) return (result.Player, null);
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
    }
}