using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    [Group]
    public class ChatterBotCommands : MewdekoSubmodule<ChatterBotService>
    {
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task Cleverbot(ITextChannel? chan = null)
        {
            var channel = chan ?? (ITextChannel)ctx.Channel;
            var cbid = await Service.GetCleverbotChannel(ctx.Guild.Id);
            if (cbid != 0 && cbid == channel.Id)
            {
                await Service.SetCleverbotChannel(ctx.Guild, 0);
                await ctx.Channel.SendConfirmAsync("Cleverbot has been switched off!");
                return;
            }

            if (cbid != 0 && cbid != channel.Id)
            {
                await Service.SetCleverbotChannel(ctx.Guild, channel.Id);
                await ctx.Channel.SendConfirmAsync(
                    $"Cleverbot channel has been switched to {channel.Mention}! Just remember that commands do not work in there while its enabled.");
            }

            if (cbid == 0)
            {
                await Service.SetCleverbotChannel(ctx.Guild, channel.Id);
                await ctx.Channel.SendConfirmAsync(
                    $"Cleverbot has been enabled and the channel set to {channel.Mention}! Just remember that commmands dont work in that channel while its enabled.");
            }
        }
    }
}