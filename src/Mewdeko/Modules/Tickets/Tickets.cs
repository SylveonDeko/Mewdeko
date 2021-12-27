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
    public class Tickets : MewdekoModuleBase<TicketService>
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
            bool contin = false;
            bool correct1 = false;
            CREmbed crembed = null;
            var component = new ComponentBuilder();
            var eb = new EmbedBuilder();
            ITextChannel channel;
            var msg = await ctx.Channel.SendConfirmAsync(
                "Please mention or type the ID of the channel you want the ticket message in!");
            var response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            if (new ChannelTypeReader<ITextChannel>().ReadAsync(ctx, response, _services).Result.IsSuccess)
            {
                await msg.DeleteAsync();
                channel = new ChannelTypeReader<ITextChannel>().ReadAsync(ctx, response, _services).Result.BestMatch as ITextChannel;
                if (!await PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription($"Would you like to setup a custom ticket panel embed? If not right now I'll just use the default and you can edit anytime using either {Prefix}tpedit or using the {Prefix}edit command."), ctx.User.Id))
                {
                    if (await PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription("Ok! I'll use the default for now! Would you like to setup a custom button emote and text?"), ctx.User.Id))
                    {
                        
                    }

                }
                else
                {
                    component.WithButton(label: "Support Server", url: "https://discord.gg/wB9FBMreRk",
                        style: ButtonStyle.Link);
                    component.WithButton(label: "Embed Builder", url: "https://eb.mewdeko.tech",
                        style: ButtonStyle.Link);
                    eb.WithDescription("Alright! Can you go https://eb.mewdeko.tech and setup the embed? If not we can help you in the support server: https://discord.gg/wB9FBMreRk \nOtherwise just paste the embed code if you know how!").WithOkColor();
                    var msg1 = await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: component.Build());
                    var embed = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                    if (CREmbed.TryParse(embed, out crembed))
                    {
                        await msg1.DeleteAsync();
                        if (await PromptUserConfirmAsync(new EmbedBuilder().WithDescription("Embed Recorded! Would you like to preview it?").WithOkColor(), ctx.User.Id))
                        {
                            var msg2 = await ctx.Channel.SendMessageAsync(crembed.PlainText, embed: crembed.ToEmbed().Build());
                            contin = await PromptUserConfirmAsync(
                                new EmbedBuilder().WithDescription("Is this Embed okay?").WithOkColor(), ctx.User.Id);
                            while (contin == false)
                            {
                                eb = new EmbedBuilder().WithDescription("Alright, please enter the embed code again!")
                                    .WithOkColor();
                                await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: component.Build());
                                embed = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                                if (!CREmbed.TryParse(embed, out crembed)) continue;
                                    await ctx.Channel.SendMessageAsync(crembed.PlainText, embed: crembed.ToEmbed().Build());
                                    contin = await PromptUserConfirmAsync(
                                        new EmbedBuilder().WithDescription("Is this Embed okay?").WithOkColor(), ctx.User.Id);
                            }
                        }
                    }
                }
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