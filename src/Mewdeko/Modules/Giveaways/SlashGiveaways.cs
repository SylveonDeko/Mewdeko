using Discord;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Giveaways.Services;
using System.Collections.Generic;

namespace Mewdeko.Modules.Giveaways;
[Discord.Interactions.Group("giveaways", "Create or manage giveaways!")]
public class SlashGiveaways : MewdekoSlashModuleBase<GiveawayService>
{
    private readonly IServiceProvider _servs;
    private readonly DbService _db;
    private readonly InteractiveService _interactivity;

    public SlashGiveaways(DbService db, IServiceProvider servs, InteractiveService interactiveService)
    {
        _interactivity = interactiveService;
        _db = db;
        _servs = servs;
    }

    [SlashCommand("emote", "Set the giveaway emote!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GEmote(string maybeEmote)
    {
        await DeferAsync();
        var emote = maybeEmote.ToIEmote();
        if (emote.Name == null)
        {
            await ctx.Interaction.SendErrorFollowupAsync("That emote is invalid!");
            return;
        }
        try
        {
            var message = await ctx.Interaction.SendConfirmFollowupAsync("Checking emote...");
            await message.AddReactionAsync(emote);
        }
        catch
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                "I'm unable to use that emote for giveaways! Most likely because I'm not in a server with it.");
            return;
        }

        await Service.SetGiveawayEmote(ctx.Guild, emote.ToString());
        await ctx.Interaction.SendConfirmFollowupAsync(
            $"Giveaway emote set to {emote}! Just keep in mind this doesn't update until the next giveaway.");
    }
    [SlashCommand("reroll", "Rerolls a giveaway!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GReroll(ulong messageid)
    {
        using var uow = _db.GetDbContext();
        var gway = uow.Giveaways
                      .GiveawaysForGuild(ctx.Guild.Id).ToList().FirstOrDefault(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Interaction.SendErrorAsync("No Giveaway with that message ID exists! Please try again!");
            return;
        }

        if (gway.Ended != 1)
        {
            await ctx.Interaction.SendErrorAsync("This giveaway hasn't ended yet!");
            return;
        }

        await Service.GiveawayReroll(gway);
        await ctx.Interaction.SendConfirmAsync("Giveaway Rerolled!");
    }

    [SlashCommand("stats", "View giveaway stats!"), CheckPermissions]
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

            await ctx.Interaction.RespondAsync(embed: eb.Build());
        }
    }

    [SlashCommand("start", "Start a giveaway!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GStart(ITextChannel chan, TimeSpan time, int winners, string what)
    {
        await ctx.Interaction.DeferAsync();
        var emote = Service.GetGiveawayEmote(ctx.Guild.Id).ToIEmote();
        try
        {
            var message = await ctx.Interaction.SendConfirmFollowupAsync("Checking emote...");
            await message.AddReactionAsync(emote);
        }
        catch
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                "I'm unable to use that emote for giveaways! Most likely because I'm not in a server with it.");
            return;
        }

        var user = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
        var perms = user.GetPermissions(chan);
        if (!perms.Has(ChannelPermission.AddReactions))
        {
            await ctx.Interaction.SendErrorFollowupAsync("I cannot add reactions in that channel!");
            return;
        }

        if (!perms.Has(ChannelPermission.UseExternalEmojis) && !ctx.Guild.Emotes.Contains(emote))
        {
            await ctx.Interaction.SendErrorFollowupAsync("I'm unable to use external emotes!");
            return;
        }
        await Service.GiveawaysInternal(chan, time, what, winners, ctx.User.Id, ctx.Guild.Id,
            ctx.Channel as ITextChannel, ctx.Guild);
    }
    

    [SlashCommand("list", "View current giveaways!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
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

        await _interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60));

        Task<PageBuilder> PageFactory(int page) => Task.FromResult(new PageBuilder().WithOkColor().WithTitle($"{gways.Count()} Active Giveaways").WithDescription(
                string.Join("\n\n",
                    gways.Skip(page * 5).Take(5).Select(x =>
                        $"{x.MessageId}"
                        + $"\nPrize: {x.Item}"
                        + $"\nWinners: {x.Winners}"
                        + $"\nLink: {ctx.Guild.GetTextChannelAsync(x.ChannelId).Result.GetMessageAsync(x.MessageId).Result.GetJumpUrl()}"))));


    }

    [SlashCommand("end", "End a giveaway!"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
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