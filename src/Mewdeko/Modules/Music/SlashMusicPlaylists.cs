using System.Threading;
using DataModel;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;

namespace Mewdeko.Modules.Music;

public partial class SlashMusic
{
    /// <summary>
    ///     Slash commands for saving, loading, listing and deleting named playlists built from the music queue.
    /// </summary>
    [Group("playlist", "Save and load music playlists")]
    public class MusicPlaylists(
        IAudioService service,
        IDataCache cache,
        GuildSettingsService guildSettingsService) : MewdekoSlashSubmodule
    {
        /// <summary>
        ///     Saves the current queue as a named playlist. Playlists are saved per guild.
        /// </summary>
        /// <param name="name">The name to save the playlist as.</param>
        [SlashCommand("save", "Saves the current queue as a named playlist")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task SavePlaylist([Summary("name", "The name to save the playlist as")] string name)
        {
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is null)
            {
                await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync();

            var (_, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendPlayerErrorAsync(result).ConfigureAwait(false);
                return;
            }

            var queue = await cache.GetMusicQueue(ctx.Guild.Id);
            if (queue.Count == 0)
            {
                await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var playlist = new MusicPlaylist
            {
                Name = name,
                AuthorId = ctx.User.Id,
                MusicPlaylistTracks = queue.Select(x => new MusicPlaylistTrack
                {
                    Title = x.Track.Title, Uri = x.Track.Uri.ToString(), Duration = x.Track.Duration
                }).ToList()
            };

            await cache.SavePlaylist(ctx.Guild.Id, playlist).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicPlaylistSaved(ctx.Guild.Id, name, queue.Count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Loads a previously saved playlist into the queue, optionally clearing the current queue first.
        /// </summary>
        /// <param name="name">The name of the playlist to load.</param>
        /// <param name="clear">Whether to clear the current queue before loading. Defaults to false.</param>
        [SlashCommand("load", "Loads a saved playlist into the queue")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task LoadPlaylist(
            [Summary("name", "The name of the playlist to load")]
            string name,
            [Summary("clear", "Clear the current queue before loading")]
            bool clear = false)
        {
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is null)
            {
                await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync();

            var (player, result) = await GetPlayerAsync();
            if (result is not null)
            {
                await SendPlayerErrorAsync(result).ConfigureAwait(false);
                return;
            }

            var playlist = await cache.GetPlaylist(ctx.Guild.Id, name);
            if (playlist == null)
            {
                await ReplyErrorAsync(Strings.MusicPlaylistNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
                return;
            }

            var queue = clear ? [] : await cache.GetMusicQueue(ctx.Guild.Id);
            var startIndex = queue.Count + 1;

            foreach (var savedTrack in playlist.MusicPlaylistTracks)
            {
                var trackResult = await service.Tracks.LoadTrackAsync(savedTrack.Uri, TrackSearchMode.YouTube);
                if (trackResult is null) continue;

                queue.Add(new MewdekoTrack(startIndex++, trackResult, new PartialUser
                {
                    Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
                }));
            }

            await cache.SetMusicQueue(ctx.Guild.Id, queue);

            if (player.CurrentItem is null && queue.Count > 0)
            {
                await player.PlayAsync(queue[0].Track).ConfigureAwait(false);
                await cache.SetCurrentTrack(ctx.Guild.Id, queue[0]);
            }

            await ReplyConfirmAsync(
                Strings.MusicPlaylistLoaded(ctx.Guild.Id, name, playlist.MusicPlaylistTracks.Count(),
                    playlist.AuthorId)
            ).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all saved playlists for the guild.
        /// </summary>
        [SlashCommand("list", "Lists all saved playlists for this server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Playlists()
        {
            await DeferAsync();

            var playlists = await cache.GetPlaylists(ctx.Guild.Id);
            if (!playlists.Any())
            {
                await ReplyErrorAsync(Strings.MusicNoPlaylists(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlaylistsTitle(ctx.Guild.Id)}")
                ], Mewdeko.OkColor)
                .WithSeparator();

            foreach (var playlist in playlists)
            {
                var user = await ctx.Guild.GetUserAsync(playlist.AuthorId);
                components.WithContainer(new TextDisplayBuilder(Strings.MusicPlaylistEntry(
                    ctx.Guild.Id,
                    playlist.Name,
                    playlist.MusicPlaylistTracks.Count(),
                    user?.Username ?? "Unknown"
                )));
            }

            await FollowupAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a saved playlist.
        /// </summary>
        /// <param name="name">The name of the playlist to remove.</param>
        [SlashCommand("delete", "Removes a saved playlist")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task DeletePlaylist([Summary("name", "The name of the playlist to remove")] string name)
        {
            var success = await cache.DeletePlaylist(ctx.Guild.Id, name);
            if (success)
                await ReplyConfirmAsync(Strings.MusicPlaylistDeleted(ctx.Guild.Id, name)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.MusicPlaylistNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
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
                    await result.Player.SetVolumeAsync(await result.Player.GetVolume() / 100f)
                        .ConfigureAwait(false);
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
}