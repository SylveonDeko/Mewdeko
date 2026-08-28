using System.Text.RegularExpressions;
using Mewdeko.Common.TriggerPlaceholders;

namespace Mewdeko.Modules.Chat_Triggers.Services;

/// <summary>
///     Supplies counter placeholders to chat trigger responses.
/// </summary>
/// <remarks>
///     <para>Guild-wide counters, shared by everyone in the server:</para>
///     <list type="bullet">
///         <item><description><c>%counter:name%</c> reads the counter.</description></item>
///         <item><description><c>%counter:name+%</c> adds one, then reads it.</description></item>
///         <item><description><c>%counter:name-%</c> subtracts one, then reads it.</description></item>
///     </list>
///     <para>Per-user counters use <c>%usercounter:name%</c> with the same suffixes.</para>
/// </remarks>
public sealed partial class CounterPlaceholderProvider : INService, ITriggerPlaceholderProvider
{
    private readonly TriggerCounterService counters;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CounterPlaceholderProvider" /> class.
    /// </summary>
    /// <param name="counters">The counter store the placeholders read and update.</param>
    public CounterPlaceholderProvider(TriggerCounterService counters)
    {
        this.counters = counters;
    }

    /// <inheritdoc />
    public IEnumerable<(string Name, Func<TriggerPlaceholderContext, Task<string?>> Func)> GetPlaceholders()
    {
        return [];
    }

    /// <inheritdoc />
    public IEnumerable<(Regex Pattern, Func<Match, TriggerPlaceholderContext, Task<string?>> Func)>
        GetRegexPlaceholders()
    {
        yield return (CounterRegex(), (match, ctx) => Resolve(match, ctx, false));
        yield return (UserCounterRegex(), (match, ctx) => Resolve(match, ctx, true));
    }

    private async Task<string?> Resolve(Match match, TriggerPlaceholderContext ctx, bool perUser)
    {
        if (ctx.Guild is null)
            return "";

        var name = match.Groups["name"].Value.ToLowerInvariant();
        var op = match.Groups["op"].Value;
        var userId = perUser ? ctx.User.Id : 0;

        var value = op switch
        {
            "+" => await counters.AddAsync(ctx.Guild.Id, name, 1, userId).ConfigureAwait(false),
            "-" => await counters.AddAsync(ctx.Guild.Id, name, -1, userId).ConfigureAwait(false),
            _ => await counters.GetAsync(ctx.Guild.Id, name, userId).ConfigureAwait(false)
        };

        return value.ToString("N0");
    }

    [GeneratedRegex(@"%counter:(?<name>[\w-]{1,64})(?<op>[+-]?)%", RegexOptions.IgnoreCase)]
    private static partial Regex CounterRegex();

    [GeneratedRegex(@"%usercounter:(?<name>[\w-]{1,64})(?<op>[+-]?)%", RegexOptions.IgnoreCase)]
    private static partial Regex UserCounterRegex();
}