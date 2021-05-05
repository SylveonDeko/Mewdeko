using Discord.Commands;
using NadekoBot.Core.Modules.Searches.Services;
using NadekoBot.Modules;

namespace NadekoBot.Core.Modules.Searches
{
    public partial class Searches
    {
        // [Group]
        // public class YtTrackCommands : NadekoSubmodule<YtTrackService>
        // {
        //     [NadekoCommand, Usage, Description, Aliases]
        //     [RequireContext(ContextType.Guild)]
        //     public async Task YtFollow(string ytChannelId, [Leftover] string uploadMessage = null)
        //     {
        //         var succ = await _service.ToggleChannelFollowAsync(Context.Guild.Id, Context.Channel.Id, ytChannelId, uploadMessage);
        //         if(succ)
        //         {
        //             await ReplyConfirmLocalizedAsync("yt_follow_added").ConfigureAwait(false);
        //         }
        //         else
        //         {
        //             await ReplyConfirmLocalizedAsync("yt_follow_fail").ConfigureAwait(false);
        //         }
        //     }
        //     
        //     [NadekoCommand, Usage, Description, Aliases]
        //     [RequireContext(ContextType.Guild)]
        //     public async Task YtTrackRm(int index)
        //     {
        //         //var succ = await _service.ToggleChannelTrackingAsync(Context.Guild.Id, Context.Channel.Id, ytChannelId, uploadMessage);
        //         //if (succ)
        //         //{
        //         //    await ReplyConfirmLocalizedAsync("yt_track_added").ConfigureAwait(false);
        //         //}
        //         //else
        //         //{
        //         //    await ReplyConfirmLocalizedAsync("yt_track_fail").ConfigureAwait(false);
        //         //}
        //     }
        //     
        //     [NadekoCommand, Usage, Description, Aliases]
        //     [RequireContext(ContextType.Guild)]
        //     public async Task YtTrackList()
        //     {
        //         //var succ = await _service.ToggleChannelTrackingAsync(Context.Guild.Id, Context.Channel.Id, ytChannelId, uploadMessage);
        //         //if (succ)
        //         //{
        //         //    await ReplyConfirmLocalizedAsync("yt_track_added").ConfigureAwait(false);
        //         //}
        //         //else
        //         //{
        //         //    await ReplyConfirmLocalizedAsync("yt_track_fail").ConfigureAwait(false);
        //         //}
        //     }
        // }
    }
}
