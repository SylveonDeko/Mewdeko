using Mewdeko.Common.TriggerPlaceholders;
using Mewdeko.Modules.Achievements.Common;

namespace Mewdeko.Modules.Achievements.Services;

/// <summary>
///     Supplies achievement placeholders to chat trigger responses.
/// </summary>
public sealed class AchievementPlaceholderProvider : INService, ITriggerPlaceholderProvider
{
    private readonly AchievementService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementPlaceholderProvider" /> class.
    /// </summary>
    /// <param name="service">The achievement service the placeholders read from.</param>
    public AchievementPlaceholderProvider(AchievementService service)
    {
        this.service = service;
    }

    /// <inheritdoc />
    public IEnumerable<(string Name, Func<TriggerPlaceholderContext, Task<string?>> Func)> GetPlaceholders()
    {
        yield return ("%achievements.points%", ctx => Read(ctx, false, s => s.Points.ToString("N0")));
        yield return ("%achievements.unlocked%", ctx => Read(ctx, false, s => s.Unlocked.ToString("N0")));
        yield return ("%achievements.tier%", ctx => Read(ctx, false, s => s.Tier.Name));
        yield return ("%achievements.rank%", ctx => Read(ctx, false, s => s.Rank.ToString()));
        yield return ("%targetuser.achievements.points%", ctx => Read(ctx, true, s => s.Points.ToString("N0")));
        yield return ("%targetuser.achievements.unlocked%", ctx => Read(ctx, true, s => s.Unlocked.ToString("N0")));
        yield return ("%targetuser.achievements.tier%", ctx => Read(ctx, true, s => s.Tier.Name));
    }

    private async Task<string?> Read(TriggerPlaceholderContext ctx, bool target,
        Func<AchievementMemberSummary, string> select)
    {
        var user = target ? ctx.Target : ctx.User;
        if (ctx.Guild is null || user is null)
            return "";

        var summary = await service.GetSummaryAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
        return select(summary);
    }
}
