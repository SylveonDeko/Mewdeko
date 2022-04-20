using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

public class ToneTags : MewdekoModuleBase<ToneTagService>
{
    [Cmd, Aliases]
    public async Task ResolveToneTags([Remainder] string tag)
    {
        var embed = Service.GetEmbed(Service.ParseTags(tag), ctx.Guild);
        await ctx.Channel.EmbedAsync(embed);
    }
}