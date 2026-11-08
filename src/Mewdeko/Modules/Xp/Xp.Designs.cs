using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;

namespace Mewdeko.Modules.Xp;

public partial class Xp
{
    /// <summary>
    ///     Equips a card design you own or that is free.
    /// </summary>
    /// <param name="design">The design name.</param>
    /// <example>.equip Sunset</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Equip([Remainder] string design)
    {
        var (ok, message) = await designViews.EquipAsync(ctx.Guild.Id, ctx.User.Id, design);
        await (ok ? ReplyConfirmAsync(message) : ReplyErrorAsync(message));
    }

    /// <summary>
    ///     Removes your equipped design for the rank or achievement card.
    /// </summary>
    /// <param name="card">Either rank or achievement.</param>
    /// <example>.unequipdesign rank</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task UnequipDesign(string card)
    {
        var (ok, message) = await designViews.UnequipAsync(ctx.Guild.Id, ctx.User.Id, card);
        await (ok ? ReplyConfirmAsync(message) : ReplyErrorAsync(message));
    }

    /// <summary>
    ///     Shows the status of the designs you made yourself.
    /// </summary>
    /// <example>.mydesign</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task MyDesign()
    {
        var (title, text) = await designViews.MyDesignsAsync(ctx.Guild.Id, ctx.User.Id);
        await ctx.Channel.SendConfirmAsync(title, text);
    }

    /// <summary>
    ///     Lists the member designs waiting for review.
    /// </summary>
    /// <example>.memberdesigns</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task MemberDesigns()
    {
        var list = await designViews.PendingAsync(ctx.Guild.Id);
        if (list is not { } result)
        {
            await ReplyConfirmAsync(Strings.MemberDesignNothingPending(ctx.Guild.Id));
            return;
        }

        await ctx.Channel.SendConfirmAsync(result.Title, result.Text);
    }

    /// <summary>
    ///     Approves a member's uploaded card background.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="card">Either rank or achievement.</param>
    /// <example>.approvedesign @user rank</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task ApproveDesign(IUser user, string card)
    {
        var (ok, message) = await designViews.ApproveAsync(ctx.Guild.Id, user.Id, card);
        await (ok ? ReplyConfirmAsync(message) : ReplyErrorAsync(message));
    }

    /// <summary>
    ///     Turns down a member's uploaded card background and tells them.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="card">Either rank or achievement.</param>
    /// <param name="reason">Why it was turned down.</param>
    /// <example>.rejectdesign @user rank Not suitable for this community</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task RejectDesign(IUser user, string card, [Remainder] string? reason = null)
    {
        var (ok, message) = await designViews.RejectAsync(ctx.Guild.Id, user.Id, card, reason);
        await (ok ? ReplyConfirmAsync(message) : ReplyErrorAsync(message));
    }

    /// <summary>
    ///     Lists the card designs you can equip.
    /// </summary>
    /// <example>.designs</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Designs()
    {
        var list = await designViews.ListAsync(ctx.Guild.Id, ctx.User.Id);
        if (list is not { } result)
        {
            await ReplyErrorAsync(Strings.DesignNone(ctx.Guild.Id));
            return;
        }

        await ctx.Channel.SendConfirmAsync(result.Title, result.Text);
    }
}
