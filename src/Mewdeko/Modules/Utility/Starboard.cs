using System.Threading.Tasks;
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
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [UserPerm(GuildPermission.ManageChannels)]
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

        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task SetStars(ulong num)
        {
            var count = Service.GetStarSetting(ctx.Guild.Id);
            await Service.SetStarCount(ctx.Guild, num);
            var count2 = Service.GetStarSetting(ctx.Guild.Id);
            await ctx.Channel.SendConfirmAsync(
                $"Your star count was successfully changed from {count} to {count2}!");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task SetStar(string num = null)
        {
            var emote = ctx.Message.Tags.Where(x => x.Type == TagType.Emoji).Select(t => (Emote) t.Value);
            try
            {
                if (num is not null) await ctx.Guild.GetEmoteAsync(emote.FirstOrDefault().Id);
            }
            catch
            {
                await ctx.Channel.SendErrorAsync("You may only use an emote in this server!");
                return;
            }

            if (num != null && Service.GetStar(ctx.Guild.Id) == emote.FirstOrDefault().Id)
            {
                await ctx.Channel.SendErrorAsync("This is already your starboard emote!");
                return;
            }

            if (num is null && Service.GetStar(ctx.Guild.Id) != 0)
            {
                await Service.SetStar(ctx.Guild, 0);
                await ctx.Channel.SendConfirmAsync("Your starboard emote has been set back to a star!");
                return;
            }

            if (Service.GetStar(ctx.Guild.Id) != 0)
            {
                var emote1 = await ctx.Guild.GetEmoteAsync(Service.GetStar(ctx.Guild.Id));
                await Service.SetStar(ctx.Guild, emote.FirstOrDefault().Id);
                var emote2 = await ctx.Guild.GetEmoteAsync(Service.GetStar(ctx.Guild.Id));
                await ctx.Channel.SendConfirmAsync(
                    $"Your starboard emote has been changed from {emote1} to {emote2}");
            }

            if (Service.GetStar(ctx.Guild.Id) == 0 && emote.Count() == 1)
            {
                await Service.SetStar(ctx.Guild, emote.FirstOrDefault().Id);
                await ctx.Channel.SendConfirmAsync(
                    $"Your starboard emote has been changed to {emote.FirstOrDefault()}");
            }
        }
    }
}