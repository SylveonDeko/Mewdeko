using System.Text;
using System.Threading;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Tracks;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Mewdeko.Modules.Music;

/// <summary>
/// Slash commands for music and button handling for music.
/// </summary>
[Group("music", "Commands for playing music!")]
public class SlashMusic(
    IAudioService service,
    IDataCache cache,
    InteractiveService interactivity,
    GuildSettingsService guildSettingsService) : MewdekoSlashCommandModule
{
    /// <summary>
    /// Handling track selection for the play command select menu.
    /// </summary>
    /// <param name="selectedValue">The selected track.</param>
    [ComponentInteraction("track_select:*", true), CheckPermissions]
    public async Task TrackSelect(ulong userId, string[] selectedValue)
    {
        await DeferAsync();

        if (ctx.User.Id != userId) return;

        var componentInteraction = ctx.Interaction as IComponentInteraction;

        var (player, result) = await GetPlayerAsync(false);

        var tracks = await cache.Redis.GetDatabase()
            .StringGetAsync($"{ctx.User.Id}_{componentInteraction.Message.Id}_tracks");

        var trackList = JsonSerializer.Deserialize<List<LavalinkTrack>>(tracks);

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);

        var selectedTracks = selectedValue.Select(i => trackList[Convert.ToInt32(i.Split("_")[1])]).ToList();

        var startIndex = queue.Count + 1;
        queue.AddRange(
            selectedTracks.Select(track => new MewdekoTrack(startIndex++, track, new PartialUser
            {
                Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
            })));

        if (selectedTracks.Count == 1)
        {
            var eb = new EmbedBuilder()
                .WithAuthor(GetText("music_added"))
                .WithDescription($"[{selectedTracks[0].Title}]({selectedTracks[0].Uri}) by {selectedTracks[0].Author}")
                .WithImageUrl(selectedTracks[0].ArtworkUri.ToString())
                .WithOkColor();

            await FollowupAsync(embed: eb.Build());
        }
        else
        {
            var paginator = new LazyPaginatorBuilder().AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(queue.Count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(5),
                responseType: InteractionResponseType.DeferredChannelMessageWithSource);

            async Task<PageBuilder> PageFactory(int index)
            {
                var tracks = queue.Skip(index * 10).Take(10).ToList();
                var sb = new StringBuilder();
                foreach (var track in tracks)
                {
                    sb.AppendLine($"{track.Index}. [{track.Track.Title}]({track.Track.Uri})");
                }

                return new PageBuilder()
                    .WithTitle($"Queue - {queue.Count} tracks")
                    .WithDescription(sb.ToString())
                    .WithOkColor();
            }
        }

        if (player.CurrentItem == null)
        {
            await cache.SetCurrentTrack(ctx.Guild.Id,
                new MewdekoTrack(1, selectedTracks[0], new PartialUser
                {
                    Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
                }));
            await player.PlayAsync(selectedTracks[0]);
        }

        await cache.SetMusicQueue(ctx.Guild.Id, queue);
        await cache.Redis.GetDatabase().KeyDeleteAsync($"{ctx.User.Id}_{componentInteraction.Message.Id}_tracks");
        await ctx.Channel.DeleteMessageAsync(componentInteraction.Message.Id);
    }


    private async ValueTask<(MewdekoPlayer, string?)> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        var channelBehavior = connectToVoiceChannel
            ? PlayerChannelBehavior.Join
            : PlayerChannelBehavior.None;

        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior);

        var options = new MewdekoPlayerOptions
        {
            Channel = ctx.Channel as ITextChannel
        };

        var result = await service.Players
            .RetrieveAsync<MewdekoPlayer, MewdekoPlayerOptions>(Context, CreatePlayerAsync, options, retrieveOptions)
            .ConfigureAwait(false);

        await result.Player.SetVolumeAsync(await result.Player.GetVolume(ctx.Guild.Id) / 100f).ConfigureAwait(false);

        if (result.IsSuccess) return (result.Player, null);
        var errorMessage = result.Status switch
        {
            PlayerRetrieveStatus.UserNotInVoiceChannel => GetText("music_not_in_channel"),
            PlayerRetrieveStatus.BotNotConnected => GetText("music_bot_not_connect",
                await guildSettingsService.GetPrefix(ctx.Guild)),
            PlayerRetrieveStatus.VoiceChannelMismatch => GetText("music_voice_channel_mismatch"),
            PlayerRetrieveStatus.Success => null,
            PlayerRetrieveStatus.UserInSameVoiceChannel => null,
            PlayerRetrieveStatus.PreconditionFailed => null,
            _ => throw new ArgumentOutOfRangeException()
        };
        return (null, errorMessage);
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