using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility

    {
        public class Starboard : MewdekoSubmodule<StarboardService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task SetStarboard(ITextChannel chn = null)
            {
                if (chn is null)
                {
                    await _service.SetStarboardChannel(ctx.Guild, 0);
                    await ctx.Channel.SendMessageAsync("Starboard has been disabled.");
                    return;
                }

                await _service.SetStarboardChannel(ctx.Guild, chn.Id);
                await ctx.Channel.SendConfirmAsync($"Channel set to {chn.Mention}");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Alias]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task SetStars(ulong num)
            {
                var count = _service.GetStarSetting(ctx.Guild.Id);
                await _service.SetStarCount(ctx.Guild, num);
                var count2 = _service.GetStarSetting(ctx.Guild.Id);
                await ctx.Channel.SendConfirmAsync(
                    $"Your star count was succesfully changed from {count} to {count2}!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Alias]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task SetStar(string num = null)
            {
                var emote = ctx.Message.Tags.Where(x => x.Type == TagType.Emoji).Select(t => (Emote)t.Value);
                try
                {
                    if (num is not null) await ctx.Guild.GetEmoteAsync(emote.FirstOrDefault().Id);
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync("You may only use an emote in this server!");
                    return;
                }

                if (num != null && _service.GetStar(ctx.Guild.Id) == emote.FirstOrDefault().Id)
                {
                    await ctx.Channel.SendErrorAsync("This is already your starboard emote!");
                    return;
                }

                if (num is null && _service.GetStar(ctx.Guild.Id) != 0)
                {
                    await _service.SetStar(ctx.Guild, 0);
                    await ctx.Channel.SendConfirmAsync("Your starboard emote has been set back to a star!");
                    return;
                }

                if (_service.GetStar(ctx.Guild.Id) != 0)
                {
                    var emote1 = await ctx.Guild.GetEmoteAsync(_service.GetStar(ctx.Guild.Id));
                    await _service.SetStar(ctx.Guild, emote.FirstOrDefault().Id);
                    var emote2 = await ctx.Guild.GetEmoteAsync(_service.GetStar(ctx.Guild.Id));
                    await ctx.Channel.SendConfirmAsync(
                        $"Your starboard emote has been changed from {emote1} to {emote2}");
                }

                if (_service.GetStar(ctx.Guild.Id) == 0 && emote.Count() == 1)
                {
                    await _service.SetStar(ctx.Guild, emote.FirstOrDefault().Id);
                    await ctx.Channel.SendConfirmAsync(
                        $"Your starboard emote has been changed to {emote.FirstOrDefault()}");
                }
            }
        }
    }
}