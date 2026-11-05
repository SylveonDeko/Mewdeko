using System.Globalization;
using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     Scheduled earning boost slash commands.
/// </summary>
public partial class SlashCurrency
{
    /// <summary>
    ///     Commands for scheduling windows that raise or lower what members earn.
    /// </summary>
    [Group("boost", "Schedule earning boosts")]
    public class CurrencyBoosts(EconomyBoostService boostService) : MewdekoSlashSubmodule<ICurrencyService>
    {
        /// <summary>
        ///     Adds a window in which earnings from chosen commands are raised or lowered by a percent.
        /// </summary>
        /// <param name="name">A short name for the boost.</param>
        /// <param name="percent">The percent to add. Negative values lower earnings.</param>
        /// <param name="duration">How long each window lasts.</param>
        /// <param name="schedule">One-off, daily, weekly or monthly.</param>
        /// <param name="commands">Daily, work, crime, rob, games or a game name, separated by commas, or all.</param>
        /// <param name="start">When the first window opens in UTC. Defaults to now.</param>
        [SlashCommand("add", "Adds a window that raises or lowers earnings")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task Add([Summary("name", "A short name for the boost")] string name,
            [Summary("percent", "Percent to add, negative lowers earnings")] [MinValue(-100)] [MaxValue(1000)]
            int percent,
            [Summary("duration", "How long each window lasts")]
            TimeSpan duration,
            [Summary("schedule", "How the window repeats")]
            EconomyBoostSchedule schedule = EconomyBoostSchedule.OneOff,
            [Summary("commands", "Daily, work, crime, rob, games or a game name, separated by commas, or all")]
            string commands = "all",
            [Summary("start", "When the first window opens in UTC, defaults to now")]
            string? start = null)
        {
            if (percent == 0)
            {
                await ErrorAsync(Strings.BoostInvalidPercent(ctx.Guild.Id));
                return;
            }

            if (duration < TimeSpan.FromMinutes(1) || duration > TimeSpan.FromDays(365))
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

            var parsed = string.Empty;

            if (!string.IsNullOrWhiteSpace(commands) && !commands.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var keys = commands
                    .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.ToLowerInvariant())
                    .Distinct()
                    .ToList();

                if (keys.Count == 0 || keys.Any(x => !EconomyBoostService.BoostableCommands.Contains(x)))
                {
                    await ErrorAsync(Strings.BoostInvalidCommands(ctx.Guild.Id,
                        string.Join(", ", EconomyBoostService.BoostableCommands)));
                    return;
                }

                parsed = string.Join(',', keys);
            }

            var boost = await boostService.AddBoostAsync(new EconomyBoost
            {
                GuildId = ctx.Guild.Id,
                Name = name,
                PercentChange = percent,
                Commands = parsed,
                ScheduleType = (int)schedule,
                StartsAt = startsAt,
                DurationMinutes = (int)duration.TotalMinutes,
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
        [SlashCommand("remove", "Deletes a scheduled boost")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task Remove([Summary("name", "The name of the boost")] string name)
        {
            if (!await boostService.RemoveBoostAsync(ctx.Guild.Id, name.Trim()))
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
        [SlashCommand("toggle", "Switches a scheduled boost on or off")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task Toggle([Summary("name", "The name of the boost")] string name)
        {
            var boost = await boostService.UpdateBoostAsync(ctx.Guild.Id, name.Trim(), x => x.Enabled = !x.Enabled);

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
        [SlashCommand("list", "Lists the scheduled boosts")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task List()
        {
            var boosts = await boostService.GetBoostsAsync(ctx.Guild.Id);

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
                    string.IsNullOrWhiteSpace(boost.Commands)
                        ? Strings.BoostAllCommands(ctx.Guild.Id)
                        : boost.Commands,
                    ((EconomyBoostSchedule)boost.ScheduleType).ToString().ToLowerInvariant(),
                    new DateTimeOffset(DateTime.SpecifyKind(boost.StartsAt, DateTimeKind.Utc)).ToUnixTimeSeconds(),
                    TimeSpan.FromMinutes(boost.DurationMinutes).ToReadableDuration()));
            }

            await ctx.Interaction.RespondAsync(embed: eb.Build());
        }
    }
}
