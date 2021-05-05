using System;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Extensions;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Utility.Services;
using System.Linq;
using AngleSharp.Dom;
using Mewdeko.Common;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility

    {
        public class Starboard : MewdekoSubmodule<StarboardService>
        {
            [MewdekoCommand, Usage, Description, Aliases]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task SetStarboard(ITextChannel chn)
            {
                try
                {
                    await _service.SetStarboardChannel(ctx.Guild, chn);
                    await ctx.Channel.SendConfirmAsync($"Channel set to {chn.Mention}");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            [MewdekoCommand, Usage, Description, Alias]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task SetStars(ulong num)
            {
                var count = _service.GetStarSetting(ctx.Guild.Id);
                await _service.SetStarCount(ctx.Guild, num);
                var count2 = _service.GetStarSetting(ctx.Guild.Id);
                await ctx.Channel.SendConfirmAsync($"Your star count was succesfully changed from {count} to {count2}!");

            }
            [MewdekoCommand, Usage, Description, Alias]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task SetStar(string num = null)
            {
                var emote = ctx.Message.Tags.Where(x => x.Type == TagType.Emoji).Select(t => (Emote)t.Value);

                if (num != null && _service.GetStar(ctx.Guild.Id) == emote.FirstOrDefault().ToString())
                {
                    await ctx.Channel.SendErrorAsync("This is already your starboard emote!");
                    return;
                }
                
                if (num is null && _service.GetStar(ctx.Guild.Id) != "none")
                {
                    await _service.SetStar(ctx.Guild, "none");
                    await ctx.Channel.SendConfirmAsync($"Your starboard emote has been set back to a star!");
                    return;
                }

                if (_service.GetStar(ctx.Guild.Id) != "none")
                {
                    var emote1 = Emote.Parse(_service.GetStar(ctx.Guild.Id));
                    await _service.SetStar(ctx.Guild, emote.FirstOrDefault().ToString());
                    var emote2 = Emote.Parse(_service.GetStar(ctx.Guild.Id));
                    await ctx.Channel.SendConfirmAsync(
                        $"Your starboard emote has been changed from {emote1} to {emote2}");
                }

                if (_service.GetStar(ctx.Guild.Id) == "none" && emote.Count() == 1)
                {
                    await _service.SetStar(ctx.Guild, emote.FirstOrDefault().ToString());
                    await ctx.Channel.SendConfirmAsync(
                        $"Your starboard emote has been changed to {emote.FirstOrDefault().ToString()}");
                }
            }
        }
    }
}