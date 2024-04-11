using System.Threading;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Tracks;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Music.CustomPlayer;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Mewdeko.Modules.Music;

/// <summary>
/// Slash commands for music and button handling for music.
/// </summary>
[Group("music", "Commands for playing music!")]
public class SlashMusic(IAudioService service, IDataCache cache) : MewdekoSlashCommandModule
{
    /// <summary>
    /// Handling track selection for the play command select menu.
    /// </summary>
    /// <param name="selectedValue">The selected track.</param>
    [ComponentInteraction("track_select", true), CheckPermissions]
    public async Task TrackSelect(string[] selectedValue)
    {
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var selectedNumber = selectedValue[0].Split("_")[1];

        var (player, result) = await GetPlayerAsync(false);
        var tracks = await cache.Redis.GetDatabase()
            .StringGetAsync($"{ctx.User.Id}_{componentInteraction.Message.Id}_tracks");

        var trackList = JsonSerializer.Deserialize<List<LavalinkTrack>>(tracks);
        var selectedTrack = trackList[Convert.ToInt32(selectedNumber)];

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);

        queue.Add(selectedTrack);

        await cache.SetMusicQueue(ctx.Guild.Id, queue);

        await cache.Redis.GetDatabase().KeyDeleteAsync($"{ctx.User.Id}_{componentInteraction.Message.Id}_tracks");

        if (queue.Count == 1)
        {
            await player.PlayAsync(selectedTrack);
        }
        else
        {
            var eb = new EmbedBuilder()
                .WithDescription(
                    $"Added [{selectedTrack.Title}]({selectedTrack.Uri}) by {selectedTrack.Author} to the queue.")
                .WithThumbnailUrl(selectedTrack.ArtworkUri?.ToString())
                .WithOkColor()
                .Build();

            await ctx.Interaction.RespondAsync(embed: eb);
        }

        await componentInteraction.Message.DeleteAsync();
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

        if (result.IsSuccess) return (result.Player, null);
        var errorMessage = result.Status switch
        {
            PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
            PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
            _ => "Unknown error.",
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