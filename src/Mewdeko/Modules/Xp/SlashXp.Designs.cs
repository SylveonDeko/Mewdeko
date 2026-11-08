using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;

namespace Mewdeko.Modules.Xp;

public partial class SlashXp
{
    /// <summary>
    ///     Equips a card design you own or that is free.
    /// </summary>
    /// <param name="design">The design name.</param>
    [SlashCommand("equip", "Equips a card design you own or that is free")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task EquipDesign(
        [Summary("design", "The design to equip"), Autocomplete(typeof(CardDesignAutocompleter))]
        string design)
    {
        var (ok, message) = await designViews.EquipAsync(ctx.Guild.Id, ctx.User.Id, design);
        await (ok ? ReplyConfirmAsync(message) : ReplyErrorAsync(message));
    }

    /// <summary>
    ///     Removes your equipped design for the rank or achievement card.
    /// </summary>
    /// <param name="card">Either rank or achievement.</param>
    [SlashCommand("unequip", "Removes your equipped design for a card")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task UnequipDesign(
        [Summary("card", "The card to reset"), Choice("Rank", "rank"), Choice("Achievement", "achievement")]
        string card)
    {
        var (ok, message) = await designViews.UnequipAsync(ctx.Guild.Id, ctx.User.Id, card);
        await (ok ? ReplyConfirmAsync(message) : ReplyErrorAsync(message));
    }

    /// <summary>
    ///     Shows the status of the designs you made yourself.
    /// </summary>
    [SlashCommand("mydesign", "Shows the status of the designs you made yourself")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task MyDesign()
    {
        var (title, text) = await designViews.MyDesignsAsync(ctx.Guild.Id, ctx.User.Id);
        await EphemeralReplyConfirmAsync($"**{title}**\n{text}");
    }

    /// <summary>
    ///     Lists the member designs waiting for review.
    /// </summary>
    [SlashCommand("memberdesigns", "Lists the member designs waiting for review")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task MemberDesigns()
    {
        await DeferAsync();
        var list = await designViews.PendingAsync(ctx.Guild.Id);
        if (list is not { } result)
        {
            await ReplyConfirmAsync(Strings.MemberDesignNothingPending(ctx.Guild.Id));
            return;
        }

        await ctx.Interaction.SendConfirmAsync(result.Title, result.Text);
    }

    /// <summary>
    ///     Approves a member's uploaded card background.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="card">Either rank or achievement.</param>
    [SlashCommand("approvedesign", "Approves a member's uploaded card background")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task ApproveDesign([Summary("user", "The member")] IUser user,
        [Summary("card", "The card"), Choice("Rank", "rank"), Choice("Achievement", "achievement")]
        string card)
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
    [SlashCommand("rejectdesign", "Turns down a member's uploaded card background and tells them")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task RejectDesign([Summary("user", "The member")] IUser user,
        [Summary("card", "The card"), Choice("Rank", "rank"), Choice("Achievement", "achievement")]
        string card,
        [Summary("reason", "Why it was turned down. The member sees it")]
        string? reason = null)
    {
        var (ok, message) = await designViews.RejectAsync(ctx.Guild.Id, user.Id, card, reason);
        await (ok ? ReplyConfirmAsync(message) : ReplyErrorAsync(message));
    }

    /// <summary>
    ///     Lists the card designs you can equip.
    /// </summary>
    [SlashCommand("designs", "Lists the card designs you can equip")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Designs()
    {
        await DeferAsync();
        var list = await designViews.ListAsync(ctx.Guild.Id, ctx.User.Id);
        if (list is not { } result)
        {
            await ReplyErrorAsync(Strings.DesignNone(ctx.Guild.Id));
            return;
        }

        await ctx.Interaction.SendConfirmAsync(result.Title, result.Text);
    }
}
