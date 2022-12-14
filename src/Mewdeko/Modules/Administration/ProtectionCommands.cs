using System.Threading.Tasks;
using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    [Group]
    public class ProtectionCommands : MewdekoSubmodule<ProtectionService>
    {
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task AntiAlt()
        {
            if (await Service.TryStopAntiAlt(ctx.Guild.Id).ConfigureAwait(false))
            {
                await ReplyConfirmLocalizedAsync("prot_disable", "Anti-Alt").ConfigureAwait(false);
                return;
            }

            await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Alt").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task AntiAlt(StoopidTime minAge, PunishmentAction action,
            [Remainder] StoopidTime? punishTime = null)
        {
            var minAgeMinutes = (int)minAge.Time.TotalMinutes;
            var punishTimeMinutes = (int?)punishTime?.Time.TotalMinutes ?? 0;

            if (minAgeMinutes < 1 || punishTimeMinutes < 0)
                return;
            switch (action)
            {
                case PunishmentAction.Timeout when punishTime.Time.Days > 28:
                    await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorLocalizedAsync("timeout_needs_time").ConfigureAwait(false);
                    return;
            }

            await Service.StartAntiAltAsync(ctx.Guild.Id, minAgeMinutes, action,
                (int?)punishTime?.Time.TotalMinutes ?? 0).ConfigureAwait(false);

            await ctx.OkAsync().ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task AntiAlt(StoopidTime minAge, PunishmentAction action, [Remainder] IRole role)
        {
            var minAgeMinutes = (int)minAge.Time.TotalMinutes;

            if (minAgeMinutes < 1)
                return;

            await Service.StartAntiAltAsync(ctx.Guild.Id, minAgeMinutes, action, roleId: role.Id).ConfigureAwait(false);

            await ctx.OkAsync().ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task AntiRaid()
        {
            if (await Service.TryStopAntiRaid(ctx.Guild.Id))
                await ReplyConfirmLocalizedAsync("prot_disable", "Anti-Raid");
            else
                await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Raid");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), Priority(1)]
        public async Task AntiRaid(int userThreshold, int seconds,
            PunishmentAction action, [Remainder] StoopidTime punishTime) =>
            await InternalAntiRaid(userThreshold, seconds, action, punishTime);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), Priority(2)]
        public async Task AntiRaid(int userThreshold, int seconds, PunishmentAction action) => await InternalAntiRaid(userThreshold, seconds, action);

        private async Task InternalAntiRaid(int userThreshold, int seconds = 10,
            PunishmentAction action = PunishmentAction.Mute, StoopidTime? punishTime = null)
        {
            switch (action)
            {
                case PunishmentAction.Timeout when punishTime.Time.Days > 28:
                    await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorLocalizedAsync("timeout_needs_time").ConfigureAwait(false);
                    return;
            }

            if (action == PunishmentAction.AddRole)
            {
                await ReplyErrorLocalizedAsync("punishment_unsupported", action).ConfigureAwait(false);
                return;
            }

            if (userThreshold is < 2 or > 30)
            {
                await ReplyErrorLocalizedAsync("raid_cnt", 2, 30).ConfigureAwait(false);
                return;
            }

            if (seconds is < 2 or > 300)
            {
                await ReplyErrorLocalizedAsync("raid_time", 2, 300).ConfigureAwait(false);
                return;
            }

            if (punishTime is not null)
            {
                if (!ProtectionService.IsDurationAllowed(action))
                    await ReplyErrorLocalizedAsync("prot_cant_use_time").ConfigureAwait(false);
            }

            var time = (int?)punishTime?.Time.TotalMinutes ?? 0;
            if (time is < 0 or > 60 * 24)
                return;

            var stats = await Service.StartAntiRaidAsync(ctx.Guild.Id, userThreshold, seconds,
                action, time).ConfigureAwait(false);

            if (stats == null) return;

            await ctx.Channel.SendConfirmAsync(GetText("prot_enable", "Anti-Raid"),
                    $"{ctx.User.Mention} {GetAntiRaidString(stats)}")
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task AntiSpam()
        {
            if (await Service.TryStopAntiSpam(ctx.Guild.Id))
                await ReplyConfirmLocalizedAsync("prot_disable", "Anti-Spam");
            else
                await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Spam");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), Priority(0)]
        public async Task AntiSpam(int messageCount, PunishmentAction action, [Remainder] IRole role)
        {
            if (action != PunishmentAction.AddRole)
                return;

            await InternalAntiSpam(messageCount, action, null, role);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), Priority(1)]
        public async Task AntiSpam(int messageCount, PunishmentAction action, [Remainder] StoopidTime punishTime) => await InternalAntiSpam(messageCount, action, punishTime);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), Priority(2)]
        public async Task AntiSpam(int messageCount, PunishmentAction action) => await InternalAntiSpam(messageCount, action);

        public async Task InternalAntiSpam(int messageCount, PunishmentAction action,
            StoopidTime? timeData = null, IRole? role = null)
        {
            if (messageCount is < 2 or > 10)
                return;

            if (timeData is not null)
            {
                if (!ProtectionService.IsDurationAllowed(action))
                {
                    await ReplyErrorLocalizedAsync("prot_cant_use_time").ConfigureAwait(false);
                    return;
                }
            }

            var time = (int?)timeData?.Time.TotalMinutes ?? 0;
            if (time is < 0 or > 60 * 24)
                return;
            switch (action)
            {
                case PunishmentAction.Timeout when timeData.Time.Days > 28:
                    await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when timeData.Time == TimeSpan.Zero:
                    await ReplyErrorLocalizedAsync("timeout_needs_time").ConfigureAwait(false);
                    return;
            }

            var stats = await Service.StartAntiSpamAsync(ctx.Guild.Id, messageCount, action, time, role?.Id)
                .ConfigureAwait(false);

            await ctx.Channel.SendConfirmAsync(GetText("prot_enable", "Anti-Spam"),
                $"{ctx.User.Mention} {GetAntiSpamString(stats)}").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task AntispamIgnore()
        {
            var added = await Service.AntiSpamIgnoreAsync(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

            if (added is null)
            {
                await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Spam").ConfigureAwait(false);
                return;
            }

            await ReplyConfirmLocalizedAsync(added.Value ? "spam_ignore" : "spam_not_ignore", "Anti-Spam")
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task AntiList()
        {
            var (spam, raid, alt) = Service.GetAntiStats(ctx.Guild.Id);

            if (spam is null && raid is null && alt is null)
            {
                await ReplyConfirmLocalizedAsync("prot_none").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("prot_active"));

            if (spam != null)
            {
                embed.AddField(efb => efb.WithName("Anti-Spam")
                    .WithValue(GetAntiSpamString(spam).TrimTo(1024))
                    .WithIsInline(true));
            }

            if (raid != null)
            {
                embed.AddField(efb => efb.WithName("Anti-Raid")
                    .WithValue(GetAntiRaidString(raid).TrimTo(1024))
                    .WithIsInline(true));
            }

            if (alt is not null)
                embed.AddField("Anti-Alt", GetAntiAltString(alt), true);

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private string? GetAntiAltString(AntiAltStats alt) =>
            GetText("anti_alt_status",
                Format.Bold(alt.MinAge.ToString(@"dd\d\ hh\h\ mm\m\ ")),
                Format.Bold(alt.Action.ToString()),
                Format.Bold(alt.Counter.ToString()));

        private string? GetAntiSpamString(AntiSpamStats stats)
        {
            var settings = stats.AntiSpamSettings;
            var ignoredString = string.Join(", ", settings.IgnoredChannels.Select(c => $"<#{c.ChannelId}>"));

            if (string.IsNullOrWhiteSpace(ignoredString))
                ignoredString = "none";

            var add = "";
            if (settings.MuteTime > 0) add = $" ({TimeSpan.FromMinutes(settings.MuteTime).Humanize()})";

            return GetText("spam_stats",
                Format.Bold(settings.MessageThreshold.ToString()),
                Format.Bold(settings.Action + add),
                ignoredString);
        }

        private string? GetAntiRaidString(AntiRaidStats stats)
        {
            var actionString = Format.Bold(stats.AntiRaidSettings.Action.ToString());

            if (stats.AntiRaidSettings.PunishDuration > 0)
                actionString += $" **({TimeSpan.FromMinutes(stats.AntiRaidSettings.PunishDuration).Humanize()})**";

            return GetText("raid_stats",
                Format.Bold(stats.AntiRaidSettings.UserThreshold.ToString()),
                Format.Bold(stats.AntiRaidSettings.Seconds.ToString()),
                actionString);
        }
    }
}