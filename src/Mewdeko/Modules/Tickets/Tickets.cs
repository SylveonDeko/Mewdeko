using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Tickets.Services;

namespace Mewdeko.Modules.Tickets
{
    public class Tickets : MewdekoModule<TicketService>
    {
        private readonly IServiceProvider _services;

        public Tickets(IServiceProvider service)
        {
            _services = service;
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task TSetup()
        {
            var component = new ComponentBuilder();
            var eb = new EmbedBuilder();
            ITextChannel channel;
            var msg = await ctx.Channel.SendConfirmAsync(
                "Please mention or type the ID of the channel you want the ticket message in!");
            var response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            if (new ChannelTypeReader<ITextChannel>().ReadAsync(ctx, response, _services).Result.IsSuccess)
            {
                channel = new ChannelTypeReader<ITextChannel>().ReadAsync(ctx, response, _services).Result.BestMatch as ITextChannel;
            }
            else
            {
                eb.WithErrorColor()
                    .WithDescription(
                        "The channel ID/Mention you provided was not correct! Please restart and try again.");
                await msg.ModifyAsync(x => x.Embed = eb.Build());
            }
        } 
    }
}