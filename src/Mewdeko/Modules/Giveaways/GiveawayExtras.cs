using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Giveaways.Common;

namespace Mewdeko.Modules.Giveaways;

/// <summary>
///     Giveaway drafts, extra entries and automatic prizes.
/// </summary>
public partial class Giveaways
{
    /// <summary>
    ///     Saves a giveaway without starting it.
    /// </summary>
    /// <param name="chan">The channel the giveaway will run in</param>
    /// <param name="time">How long the giveaway runs once started</param>
    /// <param name="winners">The amount of winners</param>
    /// <param name="what">The item to be given away</param>
    /// <example>.gdraft #giveaways 1d 1 Nitro</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GDraft(ITextChannel chan, StoopidTime time, int winners, [Remainder] string what)
    {
        if (winners < 1)
        {
            await ctx.Channel.SendErrorAsync(Strings.GiveawayInvalidWinners(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        var draft = await Service.CreateDraftAsync(ctx.Guild.Id, new Giveaway
        {
            ChannelId = chan.Id,
            UserId = ctx.User.Id,
            Item = what,
            Winners = winners,
            DraftDurationSeconds = (int)time.Time.TotalSeconds
        });

        await ctx.Channel.SendConfirmAsync(Strings.GiveawayDraftSaved(ctx.Guild.Id, draft.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists saved giveaway drafts.
    /// </summary>
    /// <example>.gdrafts</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GDrafts()
    {
        var drafts = await Service.GetDraftsAsync(ctx.Guild.Id);

        if (drafts.Count == 0)
        {
            await ctx.Channel.SendErrorAsync(Strings.GiveawayNoDrafts(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.GiveawayDraftsTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", drafts.Take(25).Select(x =>
                Strings.GiveawayDraftEntry(ctx.Guild.Id, x.Id, x.Item, x.Winners, $"<#{x.ChannelId}>"))));

        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Starts a saved giveaway draft.
    /// </summary>
    /// <param name="draftId">The ID of the draft</param>
    /// <example>.gdraftstart 3</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GDraftStart(int draftId)
    {
        var draft = await Service.FindGiveawayOrDraftAsync(ctx.Guild.Id, (ulong)Math.Max(0, draftId));

        if (draft is not { Ended: GiveawayStatus.Draft })
        {
            await ctx.Channel.SendErrorAsync(Strings.GiveawayDraftNotFound(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            await Service.StartDraftAsync(draft, ctx.User.Id).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ctx.Channel.SendErrorAsync(Strings.GiveawayDraftStartFailed(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        await ctx.Channel.SendConfirmAsync(Strings.GiveawayStarted(ctx.Guild.Id, $"<#{draft.ChannelId}>"))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a saved giveaway draft.
    /// </summary>
    /// <param name="draftId">The ID of the draft</param>
    /// <example>.gdraftdelete 3</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GDraftDelete(int draftId)
    {
        if (!await Service.DeleteDraftAsync(ctx.Guild.Id, draftId))
        {
            await ctx.Channel.SendErrorAsync(Strings.GiveawayDraftNotFound(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        await ctx.Channel.SendConfirmAsync(Strings.GiveawayDraftDeleted(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gives a role extra entries in a giveaway. Use 0 to remove them.
    /// </summary>
    /// <param name="target">The message ID of a running giveaway or the ID of a draft</param>
    /// <param name="role">The role that gets extra entries</param>
    /// <param name="extra">How many extra entries members with the role get</param>
    /// <example>.gentries 3 @Booster 2</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GEntries(ulong target, IRole role, int extra)
    {
        var gway = await Service.FindGiveawayOrDraftAsync(ctx.Guild.Id, target);

        if (gway is null || gway.Ended == GiveawayStatus.Ended)
        {
            await ctx.Channel.SendErrorAsync(Strings.GiveawayNotFound(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        var entries = GiveawayExtraEntries.Parse(gway.ExtraEntryRoles);

        if (extra <= 0)
            entries.Remove(role.Id);
        else
            entries[role.Id] = extra;

        gway.ExtraEntryRoles = GiveawayExtraEntries.Format(entries);
        await Service.UpdateGiveawayOptionsAsync(gway);

        await ctx.Channel.SendConfirmAsync(extra <= 0
                ? Strings.GiveawayEntriesRemoved(ctx.Guild.Id, role.Mention)
                : Strings.GiveawayEntriesSet(ctx.Guild.Id, role.Mention, extra))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets an XP or coin prize paid to every winner automatically. Use none to remove it.
    /// </summary>
    /// <param name="target">The message ID of a running giveaway or the ID of a draft</param>
    /// <param name="type">None, xp or coins</param>
    /// <param name="amount">How much each winner gets</param>
    /// <example>.gprize 3 coins 500</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GPrize(ulong target, GiveawayPrizeType type, long amount = 0)
    {
        var gway = await Service.FindGiveawayOrDraftAsync(ctx.Guild.Id, target);

        if (gway is null || gway.Ended == GiveawayStatus.Ended)
        {
            await ctx.Channel.SendErrorAsync(Strings.GiveawayNotFound(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        if (type != GiveawayPrizeType.None && amount <= 0)
        {
            await ctx.Channel.SendErrorAsync(Strings.GiveawayInvalidPrize(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        gway.PrizeType = (int)type;
        gway.PrizeAmount = type == GiveawayPrizeType.None ? 0 : amount;
        await Service.UpdateGiveawayOptionsAsync(gway);

        await ctx.Channel.SendConfirmAsync(type == GiveawayPrizeType.None
                ? Strings.GiveawayPrizeRemoved(ctx.Guild.Id)
                : Strings.GiveawayPrizeSet(ctx.Guild.Id, amount, type.ToString().ToLowerInvariant()))
            .ConfigureAwait(false);
    }
}
