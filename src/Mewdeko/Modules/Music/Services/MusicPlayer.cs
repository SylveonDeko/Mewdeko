#nullable enable

using Lavalink4NET.Artwork;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Music.Services;

public class MusicPlayer : LavalinkPlayer
{
    private DiscordSocketClient client;
    private MusicService musicService;

    public MusicPlayer(
        DiscordSocketClient client,
        MusicService musicService)
    {
        this.client = client;
        this.musicService = musicService;
    }

    public override async Task OnTrackStartedAsync(TrackStartedEventArgs args)
    {
        var queue = musicService.GetQueue(args.Player.GuildId);
        var track = queue.Find(x => x.Identifier == args.Player.CurrentTrack.Identifier);
        LavalinkTrack? nextTrack = null;
        try
        {
            nextTrack = queue.ElementAt(queue.IndexOf(track) + 1);
        }
        catch
        {
            //ignored
        }
        var resultMusicChannelId = (await musicService.GetSettingsInternalAsync(args.Player.GuildId)).MusicChannelId;
        if (resultMusicChannelId != null)
        {
            if (client.GetChannel(
                    resultMusicChannelId.Value) is SocketTextChannel channel)
            {
                if (track.Source != null)
                {
                    using var artworkService = new ArtworkService();
                    var artWork = await artworkService.ResolveAsync(track);
                    var eb = new EmbedBuilder()
                             .WithOkColor()
                             .WithDescription($"Now playing {track.Title} by {track.Author}")
                             .WithTitle($"Track #{queue.IndexOf(track) + 1}")
                             .WithFooter(await musicService.GetPrettyInfo(args.Player, client.GetGuild(args.Player.GuildId)))
                             .WithThumbnailUrl(artWork?.AbsoluteUri);
                    if (nextTrack is not null) eb.AddField("Up Next", $"{nextTrack.Title} by {nextTrack.Author}");

                    await channel.SendMessageAsync(embed: eb.Build());
                }
            }
        }
    }

    public override async Task OnTrackEndAsync(TrackEndEventArgs args)
    {
        var queue = musicService.GetQueue(args.Player.GuildId);
        if (queue.Count > 0)
        {
            var gid = args.Player.GuildId;
            var msettings = await musicService.GetSettingsInternalAsync(gid);
            if (client.GetChannel(msettings.MusicChannelId.Value) is not ITextChannel channel)
                return;
            if (args.Reason is TrackEndReason.Stopped or TrackEndReason.CleanUp or TrackEndReason.Replaced) return;
            var currentTrack = queue.Find(x => args.Player.CurrentTrack.Identifier == x.Identifier);
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
                    await args.Player.PlayAsync(musicService.GetQueue(gid).FirstOrDefault());
                    return;
                }
                var eb1 = new EmbedBuilder()
                          .WithOkColor()
                          .WithDescription("I have reached the end of the queue!");
                await channel.SendMessageAsync(embed: eb1.Build());
                if ((await musicService.GetSettingsInternalAsync(args.Player.GuildId)).AutoDisconnect is
                    AutoDisconnect.Either or AutoDisconnect.Queue)
                {
                    await args.Player.StopAsync(true);
                    return;
                }
            }

            await args.Player.PlayAsync(nextTrack);
        }
    }
}