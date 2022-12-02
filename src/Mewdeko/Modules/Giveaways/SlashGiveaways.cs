using System.Threading.Tasks;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Giveaways.Services;

namespace Mewdeko.Modules.Giveaways;

[Group("giveaways", "Create or manage giveaways!")]
public class SlashGiveaways : MewdekoSlashModuleBase<GiveawayService>
{
    private readonly DbService db;
    private readonly InteractiveService interactivity;
    private readonly GuildSettingsService guildSettings;

    public SlashGiveaways(DbService db, InteractiveService interactiveService, GuildSettingsService guildSettings)
    {
        interactivity = interactiveService;
        this.guildSettings = guildSettings;
        this.db = db;
    }

    [SlashCommand("emote", "Set the giveaway emote!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GEmote(string maybeEmote)
    {
        await DeferAsync().ConfigureAwait(false);
        var emote = maybeEmote.ToIEmote();
        if (emote.Name == null)
        {
            await ctx.Interaction.SendErrorFollowupAsync("That emote is invalid!").ConfigureAwait(false);
            return;
        }

        try
        {
            var message = await ctx.Interaction.SendConfirmFollowupAsync("Checking emote...").ConfigureAwait(false);
            await message.AddReactionAsync(emote).ConfigureAwait(false);
        }
        catch
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                "I'm unable to use that emote for giveaways! Most likely because I'm not in a server with it.").ConfigureAwait(false);
            return;
        }

        await Service.SetGiveawayEmote(ctx.Guild, emote.ToString()).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmFollowupAsync(
            $"Giveaway emote set to {emote}! Just keep in mind this doesn't update until the next giveaway.").ConfigureAwait(false);
    }

    [SlashCommand("reroll", "Rerolls a giveaway!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GReroll(ulong messageid)
    {
        await using var uow = db.GetDbContext();
        var gway = uow.Giveaways
            .GiveawaysForGuild(ctx.Guild.Id).ToList().Find(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Interaction.SendErrorAsync("No Giveaway with that message ID exists! Please try again!").ConfigureAwait(false);
            return;
        }

        if (gway.Ended != 1)
        {
            await ctx.Interaction.SendErrorAsync("This giveaway hasn't ended yet!").ConfigureAwait(false);
            return;
        }

        await Service.GiveawayReroll(gway).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Giveaway Rerolled!").ConfigureAwait(false);
    }

    [SlashCommand("stats", "View giveaway stats!"), CheckPermissions]
    public async Task GStats()
    {
        var eb = new EmbedBuilder().WithOkColor();
        var gways = db.GetDbContext().Giveaways.GiveawaysForGuild(ctx.Guild.Id);
        if (gways.Count == 0)
        {
            await ctx.Channel.SendErrorAsync("There have been no giveaways here, so no stats!").ConfigureAwait(false);
        }
        else
        {
            List<ITextChannel> gchans = new();
            foreach (var i in gways)
            {
                var chan = await ctx.Guild.GetTextChannelAsync(i.ChannelId).ConfigureAwait(false);
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

            await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    [SlashCommand("start", "Start a giveaway!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GStart(ITextChannel chan, TimeSpan time, int winners, string what)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        var emote = (await Service.GetGiveawayEmote(ctx.Guild.Id)).ToIEmote();
        try
        {
            var message = await ctx.Interaction.SendConfirmFollowupAsync("Checking emote...").ConfigureAwait(false);
            await message.AddReactionAsync(emote).ConfigureAwait(false);
        }
        catch
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                "I'm unable to use that emote for giveaways! Most likely because I'm not in a server with it.").ConfigureAwait(false);
            return;
        }

        var user = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = user.GetPermissions(chan);
        if (!perms.Has(ChannelPermission.AddReactions))
        {
            await ctx.Interaction.SendErrorFollowupAsync("I cannot add reactions in that channel!").ConfigureAwait(false);
            return;
        }

        if (!perms.Has(ChannelPermission.UseExternalEmojis) && !ctx.Guild.Emotes.Contains(emote))
        {
            await ctx.Interaction.SendErrorFollowupAsync("I'm unable to use external emotes!").ConfigureAwait(false);
            return;
        }

        await Service.GiveawaysInternal(chan, time, what, winners, ctx.User.Id, ctx.Guild.Id,
            ctx.Channel as ITextChannel, ctx.Guild).ConfigureAwait(false);
    }

    [SlashCommand("list", "View current giveaways!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GList()
    {
        await using var uow = db.GetDbContext();
        var gways = uow.Giveaways.GiveawaysForGuild(ctx.Guild.Id).Where(x => x.Ended == 0);
        if (!gways.Any())
        {
            await ctx.Channel.SendErrorAsync("No active giveaways").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(gways.Count() / 5)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            return new PageBuilder().WithOkColor().WithTitle($"{gways.Count()} Active Giveaways")
                .WithDescription(string.Join("\n\n",
                    await gways.Skip(page * 5).Take(5).Select(async x =>
                            $"{x.MessageId}\nPrize: {x.Item}\nWinners: {x.Winners}\nLink: {await GetJumpUrl(x.ChannelId, x.MessageId).ConfigureAwait(false)}").GetResults()
                        .ConfigureAwait(false)));
        }
    }

    private async Task<string> GetJumpUrl(ulong channelId, ulong messageId)
    {
        var channel = await ctx.Guild.GetTextChannelAsync(channelId).ConfigureAwait(false);
        var message = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
        return message.GetJumpUrl();
    }

    [SlashCommand("end", "End a giveaway!"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GEnd(ulong messageid)
    {
        await using var uow = db.GetDbContext();
        var gway = uow.Giveaways
            .GiveawaysForGuild(ctx.Guild.Id).ToList().Find(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Channel.SendErrorAsync("No Giveaway with that message ID exists! Please try again!").ConfigureAwait(false);
        }

        if (gway.Ended == 1)
        {
            await ctx.Channel.SendErrorAsync(
                $"This giveaway has already ended! Plase use `{await guildSettings.GetPrefix(ctx.Guild)}greroll {messageid}` to reroll!").ConfigureAwait(false);
        }
        else
        {
            await Service.GiveawayReroll(gway).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Giveaway ended!").ConfigureAwait(false);
        }
    }
}