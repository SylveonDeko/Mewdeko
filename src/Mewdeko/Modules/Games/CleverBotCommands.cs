using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    [Group]
    public class ChatterBotCommands : MewdekoSubmodule<ChatterBotService>
    {
        private readonly Mewdeko _bot;

        public ChatterBotCommands(Mewdeko bot) 
            => _bot = bot;

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageMessages)]
        public async Task Cleverbot(ITextChannel? chan = null)
        {
            var channel = chan ?? (ITextChannel) ctx.Channel;
            var cbid = Service.GetCleverbotChannel(ctx.Guild.Id);
            if (cbid != 0 && cbid == channel.Id)
            {
                _bot.AllGuildConfigs[ctx.Guild.Id].CleverbotChannel = 0;
                await Service.SetCleverbotChannel(ctx.Guild, 0);
                await ctx.Channel.SendConfirmAsync("Cleverbot has been switched off!");
                return;
            }

            if (cbid != 0 && cbid != channel.Id)
            {
                _bot.AllGuildConfigs[ctx.Guild.Id].CleverbotChannel = channel.Id;
                await Service.SetCleverbotChannel(ctx.Guild, channel.Id);
                await ctx.Channel.SendConfirmAsync(
                    $"Cleverbot channel has been switched to {channel.Mention}! Just remember that commands do not work in there while its enabled.");
            }

            if (cbid == 0)
            {
                _bot.AllGuildConfigs[ctx.Guild.Id].CleverbotChannel = 0;
                await Service.SetCleverbotChannel(ctx.Guild, channel.Id);
                await ctx.Channel.SendConfirmAsync(
                    $"Cleverbot has been enabled and the channel set to {channel.Mention}! Just remember that commmands dont work in that channel while its enabled.");
            }
        }
    }
}