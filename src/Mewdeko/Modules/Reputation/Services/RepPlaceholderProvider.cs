using Mewdeko.Common.TriggerPlaceholders;

namespace Mewdeko.Modules.Reputation.Services;

/// <summary>
///     Supplies reputation placeholders to chat trigger responses.
/// </summary>
public sealed class RepPlaceholderProvider : INService, ITriggerPlaceholderProvider
{
    private readonly RepService repService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RepPlaceholderProvider" /> class.
    /// </summary>
    /// <param name="repService">The reputation service the placeholders read from.</param>
    public RepPlaceholderProvider(RepService repService)
    {
        this.repService = repService;
    }

    /// <inheritdoc />
    public IEnumerable<(string Name, Func<TriggerPlaceholderContext, Task<string?>> Func)> GetPlaceholders()
    {
        yield return ("%rep%", ctx => Rep(ctx, false, x => x.reputation));
        yield return ("%rep.rank%", ctx => Rep(ctx, false, x => x.rank));
        yield return ("%targetuser.rep%", ctx => Rep(ctx, true, x => x.reputation));
        yield return ("%targetuser.rep.rank%", ctx => Rep(ctx, true, x => x.rank));
    }

    private async Task<string?> Rep(TriggerPlaceholderContext ctx, bool target,
        Func<(int reputation, int rank), int> select)
    {
        var user = target ? ctx.Target : ctx.User;
        if (ctx.Guild is null || user is null)
            return "";

        var rep = await repService.GetUserReputationAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
        return select(rep).ToString();
    }
}