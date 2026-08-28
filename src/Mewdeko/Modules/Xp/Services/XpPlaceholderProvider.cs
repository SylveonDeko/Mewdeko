using Mewdeko.Common.TriggerPlaceholders;
using Mewdeko.Modules.Xp.Models;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Supplies XP placeholders to chat trigger responses.
/// </summary>
public sealed class XpPlaceholderProvider : INService, ITriggerPlaceholderProvider
{
    private readonly XpService xpService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpPlaceholderProvider" /> class.
    /// </summary>
    /// <param name="xpService">The XP service the placeholders read from.</param>
    public XpPlaceholderProvider(XpService xpService)
    {
        this.xpService = xpService;
    }

    /// <inheritdoc />
    public IEnumerable<(string Name, Func<TriggerPlaceholderContext, Task<string?>> Func)> GetPlaceholders()
    {
        yield return ("%xp.level%", ctx => Stat(ctx, false, x => x.Level.ToString()));
        yield return ("%xp.rank%", ctx => Stat(ctx, false, x => x.Rank.ToString()));
        yield return ("%xp.total%", ctx => Stat(ctx, false, x => x.TotalXp.ToString()));
        yield return ("%xp.current%", ctx => Stat(ctx, false, x => x.LevelXp.ToString()));
        yield return ("%xp.required%", ctx => Stat(ctx, false, x => x.RequiredXp.ToString()));
        yield return ("%xp.remaining%", ctx => Stat(ctx, false, x => (x.RequiredXp - x.LevelXp).ToString()));
        yield return ("%xp.bonus%", ctx => Stat(ctx, false, x => x.BonusXp.ToString()));

        yield return ("%targetuser.xp.level%", ctx => Stat(ctx, true, x => x.Level.ToString()));
        yield return ("%targetuser.xp.rank%", ctx => Stat(ctx, true, x => x.Rank.ToString()));
        yield return ("%targetuser.xp.total%", ctx => Stat(ctx, true, x => x.TotalXp.ToString()));
    }

    private async Task<string?> Stat(TriggerPlaceholderContext ctx, bool target, Func<UserXpStats, string> select)
    {
        var user = target ? ctx.Target : ctx.User;
        if (ctx.Guild is null || user is null)
            return "";

        var stats = await xpService.GetUserXpStatsAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
        return stats is null ? "0" : select(stats);
    }
}