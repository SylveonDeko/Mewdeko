using Mewdeko.Common.TriggerPlaceholders;
using Mewdeko.Modules.Afk.Services;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Supplies message count, invite count and AFK placeholders to chat trigger responses.
/// </summary>
public sealed class ActivityPlaceholderProvider : INService, ITriggerPlaceholderProvider
{
    private readonly AfkService afkService;
    private readonly InviteCountService inviteCounts;
    private readonly MessageCountService messageCounts;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ActivityPlaceholderProvider" /> class.
    /// </summary>
    /// <param name="messageCounts">The message count service.</param>
    /// <param name="inviteCounts">The invite count service.</param>
    /// <param name="afkService">The AFK service.</param>
    public ActivityPlaceholderProvider(MessageCountService messageCounts, InviteCountService inviteCounts,
        AfkService afkService)
    {
        this.messageCounts = messageCounts;
        this.inviteCounts = inviteCounts;
        this.afkService = afkService;
    }

    /// <inheritdoc />
    public IEnumerable<(string Name, Func<TriggerPlaceholderContext, Task<string?>> Func)> GetPlaceholders()
    {
        yield return ("%messages%", ctx => Messages(ctx, MessageCountService.CountQueryType.User, false));
        yield return ("%targetuser.messages%", ctx => Messages(ctx, MessageCountService.CountQueryType.User, true));
        yield return ("%messages.channel%", ctx => Messages(ctx, MessageCountService.CountQueryType.Channel, false));
        yield return ("%messages.server%", ctx => Messages(ctx, MessageCountService.CountQueryType.Guild, false));

        yield return ("%invites%", ctx => Invites(ctx, false));
        yield return ("%targetuser.invites%", ctx => Invites(ctx, true));
        yield return ("%inviter%", async ctx =>
        {
            if (ctx.Guild is null)
                return "";
            var inviter = await inviteCounts.GetInviter(ctx.User.Id, ctx.Guild).ConfigureAwait(false);
            return inviter?.Mention ?? "";
        });

        yield return ("%targetuser.afk%", async ctx =>
        {
            if (ctx.Guild is null || ctx.Target is null)
                return "";
            var afk = await afkService.GetAfk(ctx.Guild.Id, ctx.Target.Id).ConfigureAwait(false);
            return afk?.Message ?? "";
        });
    }

    private async Task<string?> Messages(TriggerPlaceholderContext ctx, MessageCountService.CountQueryType type,
        bool target)
    {
        if (ctx.Guild is null)
            return "";

        var snowflake = type switch
        {
            MessageCountService.CountQueryType.Channel => ctx.Channel?.Id ?? 0,
            MessageCountService.CountQueryType.User => (target ? ctx.Target?.Id : ctx.User.Id) ?? 0,
            _ => ctx.Guild.Id
        };

        if (snowflake == 0)
            return "";

        var count = await messageCounts.GetMessageCount(type, ctx.Guild.Id, snowflake).ConfigureAwait(false);
        return count.ToString("N0");
    }

    private async Task<string?> Invites(TriggerPlaceholderContext ctx, bool target)
    {
        var user = target ? ctx.Target : ctx.User;
        if (ctx.Guild is null || user is null)
            return "";

        var count = await inviteCounts.GetInviteCount(user.Id, ctx.Guild.Id).ConfigureAwait(false);
        return count.ToString("N0");
    }
}