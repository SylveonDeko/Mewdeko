using System.Globalization;
using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     Scheduled earning boosts.
/// </summary>
public partial class Currency
{
    /// <summary>
    ///     The service managing scheduled earning boosts.
    /// </summary>
    public EconomyBoostService BoostManager { get; set; }

    /// <summary>
    ///     Adds a window in which earnings from chosen commands are raised or lowered by a percent.
    /// </summary>
    /// <param name="name">A short name for the boost.</param>
    /// <param name="percent">The percent to add. Negative values lower earnings.</param>
    /// <param name="duration">How long each window lasts, such as 2h.</param>
    /// <param name="schedule">One-off, daily, weekly or monthly.</param>
    /// <param name="commands">Daily, work, crime, rob, games or a game name, separated by commas, or all.</param>
    /// <param name="start">When the first window opens in UTC. Defaults to now.</param>
    /// <example>.boostadd weekend 50 48h weekly all 2026-01-02 18:00</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task BoostAdd(string name, int percent, StoopidTime duration,
        EconomyBoostSchedule schedule = EconomyBoostSchedule.OneOff, string commands = "all",
        [Remainder] string? start = null)
    {
        if (percent is < -100 or > 1000 || percent == 0)
        {
            await ErrorAsync(Strings.BoostInvalidPercent(ctx.Guild.Id));
            return;
        }

        if (duration.Time < TimeSpan.FromMinutes(1) || duration.Time > TimeSpan.FromDays(365))
        {
            await ErrorAsync(Strings.BoostInvalidDuration(ctx.Guild.Id));
            return;
        }

        var startsAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(start) && !DateTime.TryParse(start, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out startsAt))
        {
            await ErrorAsync(Strings.BoostInvalidStart(ctx.Guild.Id));
            return;
        }

        if (!TryParseBoostCommands(commands, out var parsed))
        {
            await ErrorAsync(Strings.BoostInvalidCommands(ctx.Guild.Id,
                string.Join(", ", EconomyBoostService.BoostableCommands)));
            return;
        }

        var boost = await BoostManager.AddBoostAsync(new EconomyBoost
        {
            GuildId = ctx.Guild.Id,
            Name = name,
            PercentChange = percent,
            Commands = parsed,
            ScheduleType = (int)schedule,
            StartsAt = startsAt,
            DurationMinutes = (int)duration.Time.TotalMinutes,
            Enabled = true
        });

        if (boost is null)
        {
            await ErrorAsync(Strings.BoostDuplicate(ctx.Guild.Id, name));
            return;
        }

        await ConfirmAsync(Strings.BoostAdded(ctx.Guild.Id, name));
    }

    /// <summary>
    ///     Deletes a scheduled earning boost.
    /// </summary>
    /// <param name="name">The name of the boost.</param>
    /// <example>.boostremove weekend</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task BoostRemove([Remainder] string name)
    {
        if (!await BoostManager.RemoveBoostAsync(ctx.Guild.Id, name))
        {
            await ErrorAsync(Strings.BoostNotFound(ctx.Guild.Id, name));
            return;
        }

        await ConfirmAsync(Strings.BoostRemoved(ctx.Guild.Id, name));
    }

    /// <summary>
    ///     Switches a scheduled earning boost on or off without deleting it.
    /// </summary>
    /// <param name="name">The name of the boost.</param>
    /// <example>.boosttoggle weekend</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task BoostToggle([Remainder] string name)
    {
        var boost = await BoostManager.UpdateBoostAsync(ctx.Guild.Id, name, x => x.Enabled = !x.Enabled);

        if (boost is null)
        {
            await ErrorAsync(Strings.BoostNotFound(ctx.Guild.Id, name));
            return;
        }

        await ConfirmAsync(boost.Enabled
            ? Strings.BoostEnabled(ctx.Guild.Id, boost.Name)
            : Strings.BoostDisabled(ctx.Guild.Id, boost.Name));
    }

    /// <summary>
    ///     Lists the server's scheduled earning boosts.
    /// </summary>
    /// <example>.boostlist</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task BoostList()
    {
        var boosts = await BoostManager.GetBoostsAsync(ctx.Guild.Id);

        if (boosts.Count == 0)
        {
            await ErrorAsync(Strings.BoostNone(ctx.Guild.Id));
            return;
        }

        var now = DateTime.UtcNow;
        var eb = new EmbedBuilder().WithOkColor().WithTitle(Strings.BoostListTitle(ctx.Guild.Id));

        foreach (var boost in boosts.Take(25))
        {
            var state = !boost.Enabled
                ? Strings.BoostStateOff(ctx.Guild.Id)
                : EconomyBoostService.IsActive(boost, now)
                    ? Strings.BoostStateActive(ctx.Guild.Id)
                    : Strings.BoostStateWaiting(ctx.Guild.Id);

            eb.AddField($"{boost.Name} ({state})", Strings.BoostDetails(ctx.Guild.Id, boost.PercentChange,
                string.IsNullOrWhiteSpace(boost.Commands) ? Strings.BoostAllCommands(ctx.Guild.Id) : boost.Commands,
                ((EconomyBoostSchedule)boost.ScheduleType).ToString().ToLowerInvariant(),
                new DateTimeOffset(DateTime.SpecifyKind(boost.StartsAt, DateTimeKind.Utc)).ToUnixTimeSeconds(),
                TimeSpan.FromMinutes(boost.DurationMinutes).ToReadableDuration()));
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    private static bool TryParseBoostCommands(string input, out string parsed)
    {
        parsed = string.Empty;

        if (string.IsNullOrWhiteSpace(input) || input.Equals("all", StringComparison.OrdinalIgnoreCase))
            return true;

        var keys = input.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (keys.Count == 0 || keys.Any(x => !EconomyBoostService.BoostableCommands.Contains(x)))
            return false;

        parsed = string.Join(',', keys);
        return true;
    }
}
