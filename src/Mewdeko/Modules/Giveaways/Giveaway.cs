using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Giveaways.Services;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Giveaways;

public class Giveaways : MewdekoModuleBase<GiveawayService>
{
    private readonly IServiceProvider _servs;
    private readonly DbService _db;
    private readonly InteractiveService Interactivity;

    public Giveaways(DbService db, IServiceProvider servs, InteractiveService interactiveService)
    {
        Interactivity = interactiveService;
        _db = db;
        _servs = servs;
    }

    [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task GReroll(ulong messageid)
    {
        using var uow = _db.GetDbContext();
        var gway = uow.Giveaways
                      .GiveawaysForGuild(ctx.Guild.Id).ToList().FirstOrDefault(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Channel.SendErrorAsync("No Giveaway with that message ID exists! Please try again!");
            return;
        }

        await Service.GiveawayReroll(gway);
        await ctx.Channel.SendConfirmAsync("Giveaway Rerolled!");
    }

    [MewdekoCommand, Usage, Description, Aliases]
    public async Task GStats()
    {
        var eb = new EmbedBuilder().WithOkColor();
        var gways = _db.GetDbContext().Giveaways.GiveawaysForGuild(ctx.Guild.Id);
        if (!gways.Any())
        {
            await ctx.Channel.SendErrorAsync("There have been no giveaways here, so no stats!");
        }
        else
        {
            List<ITextChannel> gchans = new();
            foreach (var i in gways)
            {
                var chan = await ctx.Guild.GetTextChannelAsync(i.ChannelId);
                if (!gchans.Contains(chan))
                    gchans.Add(chan);
            }

            var amount = gways.Distinct(x => x.UserId).Count();
            eb.WithTitle("Giveaway Statistics!");
            eb.AddField("Amount of users that started giveaways", amount, true);
            eb.AddField("Total amount of giveaways", gways.Count, true);
            eb.AddField("Active Giveaways", gways.Count(x => x.Ended == 0), true);
            eb.AddField("Ended Giveaways", gways.Count(x => x.Ended == 1), true);
            eb.AddField("Giveaway Channels: Uses",
                string.Join("\n", gchans.Select(x => $"{x.Mention}: {gways.Count(s => s.ChannelId == x.Id)}")),
                true);

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }
    }

    [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task GStart(ITextChannel chan, StoopidTime time, int winners, [Remainder] string what) =>
        await Service.GiveawaysInternal(chan, time.Time, what, winners, ctx.User.Id, ctx.Guild.Id,
            ctx.Channel as ITextChannel, ctx.Guild);

    [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task GStart()
    {
        ITextChannel chan = null;
        var winners = 0;
        string prize;
        //string blacklistroles;
        //string blacklistusers;
        string reqroles;
        IUser host;
        TimeSpan time;
        var erorrembed = new EmbedBuilder()
            .WithErrorColor()
            .WithDescription("Either something went wrong or you input a value incorrectly! Please start over.")
            .Build();
        var win0embed = new EmbedBuilder()
            .WithErrorColor()
            .WithDescription("You can't have 0 winners!").Build();
        var tries = 0;
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithDescription(
                "Please say, mention or put the ID of the channel where you want to start a giveaway. (Keep in mind you can cancel this by just leaving this to sit)");
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
        var next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        var reader = new ChannelTypeReader<ITextChannel>();
        var e = await reader.ReadAsync(ctx, next, _servs);
        if (!e.IsSuccess)
        {
            await msg.ModifyAsync(x => x.Embed = erorrembed);
            return;
        }

        chan = (ITextChannel) e.BestMatch;
        await msg.ModifyAsync(x => x.Embed = eb.WithDescription("How many winners will there be?").Build());
        next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        try
        {
            winners = int.Parse(next);
        }
        catch
        {
            await msg.ModifyAsync(x => x.Embed = erorrembed);
            tries++;
            return;
        }

        while (tries > 0)
        {
            await msg.ModifyAsync(x => x.Embed = win0embed);
            next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            try
            {
                winners = int.Parse(next);
                tries = 0;
            }
            catch
            {
                await msg.ModifyAsync(x => x.Embed = erorrembed);
                return;
            }
        }

        await msg.ModifyAsync(x => x.Embed = eb.WithDescription("What is the prize/item?").Build());
        prize = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        await msg.ModifyAsync(x =>
            x.Embed = eb.WithDescription("How long will this giveaway last? Use the format 1mo,2d,3m,4s").Build());
        next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        {
            try
            {
                var t = StoopidTime.FromInput(next);
                time = t.Time;
            }
            catch
            {
                await msg.ModifyAsync(x => x.Embed = erorrembed);
                return;
            }
        }
        await msg.ModifyAsync(x =>
            x.Embed = eb
                .WithDescription(
                    "Who is the giveaway host? You can mention them or provide an ID, say none/skip to set yourself as the host.")
                .Build());
        next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        if (next.ToLower() == "none" || next.ToLower() == "skip")
        {
            host = ctx.User;
        }
        else
        {
            var reader1 = new UserTypeReader<IUser>();
            try
            {
                var result = await reader1.ReadAsync(ctx, next, _servs);
                host = (IUser) result.BestMatch;
            }
            catch
            {
                await msg.ModifyAsync(x => x.Embed = erorrembed);
                return;
            }
        }
        
        if (!await PromptUserConfirmAsync(msg, new EmbedBuilder().WithDescription("Would you like to setup role requirements?").WithOkColor(), ctx.User.Id))
        {
            await Service.GiveawaysInternal(chan, time, prize, winners, host.Id, ctx.Guild.Id, ctx.Channel as ITextChannel,
            ctx.Guild);
            await msg.DeleteAsync();
        }
        await msg.ModifyAsync(x =>
        {
            x.Embed = eb.WithDescription("Alright! please mention the role(s) that are required for this giveaway!")
                        .Build();
            x.Components = null;
        });
        IReadOnlyCollection<IRole> parsed;
        while (true)
        {
            next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            parsed = Regex.Matches(next, @"(?<=<@&)?[0-9]{17,19}(?=>)?")
                          .Select(m => ulong.Parse(m.Value))
                          .Select(Context.Guild.GetRole)
                          .OfType<IRole>().ToList();
            if (parsed.Any()) break;
            await msg.ModifyAsync(x => x.Embed = eb
                                                 .WithDescription("Looks like those roles were incorrect! Please try again!")
                                                 .Build());
        }

        reqroles = string.Join(" ", parsed.Select(x => x.Id));
        await msg.DeleteAsync();
        await Service.GiveawaysInternal(chan, time, prize, winners, host.Id, ctx.Guild.Id, ctx.Channel as ITextChannel,
            ctx.Guild, reqroles);
    }

    [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task GList()
    {
        using var uow = _db.GetDbContext();
        var gways = uow.Giveaways.GiveawaysForGuild(ctx.Guild.Id).Where(x => x.Ended == 0);
        if (!gways.Any())
        {
            await ctx.Channel.SendErrorAsync("No active giveaways");
            return;
        }
        var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(gways.Count() / 5)
                        .WithDefaultEmotes()
                        .Build();

        await Interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60));

        Task<PageBuilder> PageFactory(int page)
        {
            return Task.FromResult(new PageBuilder().WithOkColor().WithTitle($"{gways.Count()} Active Giveaways").WithDescription(
                string.Join("\n\n",
                    gways.Skip(page * 5).Take(5).Select(x =>
                        $"{x.MessageId}"
                        + $"\nPrize: {x.Item}"
                        + $"\nWinners: {x.Winners}"
                        + $"\nLink: {ctx.Guild.GetTextChannelAsync(x.ChannelId).Result.GetMessageAsync(x.MessageId).Result.GetJumpUrl()}"))));
        }
        

    }

    [MewdekoCommand, Aliases, RequireContext(ContextType.Guild), RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task GEnd(ulong messageid)
    {
        using var uow = _db.GetDbContext();
        var gway = uow.Giveaways
                       .GiveawaysForGuild(ctx.Guild.Id).ToList().FirstOrDefault(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Channel.SendErrorAsync("No Giveaway with that message ID exists! Please try again!");
        }

        if (gway.Ended == 1)
        {
            await ctx.Channel.SendErrorAsync(
                $"This giveaway has already ended! Plase use `{Prefix}greroll {messageid}` to reroll!");
        }
        else
        {
            await Service.GiveawayReroll(gway);
            await ctx.Channel.SendConfirmAsync("Giveaway ended!");
        }
        
    }
}