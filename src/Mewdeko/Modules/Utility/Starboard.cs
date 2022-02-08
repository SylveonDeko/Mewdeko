using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility

{
    public class Starboard : MewdekoSubmodule<StarboardService>
    {
        [MewdekoCommand, Usage, Description, Aliases, UserPerm(GuildPermission.ManageChannels)]
        public async Task SetStarboard(ITextChannel chn = null)
        {
            if (chn is null)
            {
                await Service.SetStarboardChannel(ctx.Guild, 0);
                await ctx.Channel.SendMessageAsync("Starboard has been disabled.");
                return;
            }

            await Service.SetStarboardChannel(ctx.Guild, chn.Id);
            await ctx.Channel.SendConfirmAsync($"Channel set to {chn.Mention}");
        }

        [MewdekoCommand, Usage, Description, Alias, UserPerm(GuildPermission.ManageChannels)]
        public async Task SetStars(int num)
        {
            var count = Service.GetStarCount(ctx.Guild.Id);
            await Service.SetStarCount(ctx.Guild, num);
            var count2 = Service.GetStarCount(ctx.Guild.Id);
            await ctx.Channel.SendConfirmAsync(
                $"Your star count was successfully changed from {count} to {count2}!");
        }

        [MewdekoCommand, Usage, Description, Alias, UserPerm(GuildPermission.ManageChannels)]
        public async Task SetStar(IEmote emote)
        {
            await Service.SetStar(ctx.Guild, emote.ToString());
            await ctx.Channel.SendConfirmAsync($"Successfully set the star to {emote}");
        }
    }
}