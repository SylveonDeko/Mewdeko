using System;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;
using System.Linq;
using System.Threading.Tasks;
using Mewdeko.Core.Common.TypeReaders.Models;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class ProtectionCommands : MewdekoSubmodule<ProtectionService>
        {
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task AntiAlt()
            {
                if (await _service.TryStopAntiAlt(ctx.Guild.Id))
                {
                    await ReplyConfirmLocalizedAsync("prot_disable", "Anti-Alt");
                    return;
                }

                await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Alt");
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task AntiAlt(StoopidTime minAge, PunishmentAction action, [Leftover] StoopidTime punishTime = null)
            {
                var minAgeMinutes = (int)minAge.Time.TotalMinutes;
                var punishTimeMinutes = (int?)punishTime?.Time.TotalMinutes ?? 0;

                if (minAgeMinutes < 1 || punishTimeMinutes < 0)
                    return;

                await _service.StartAntiAltAsync(ctx.Guild.Id, minAgeMinutes, action, (int?)punishTime?.Time.TotalMinutes ?? 0);

                await ctx.OkAsync();
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task AntiAlt(StoopidTime minAge, PunishmentAction action, [Leftover] IRole role)
            {
                var minAgeMinutes = (int)minAge.Time.TotalMinutes;

                if (minAgeMinutes < 1)
                    return;

                await _service.StartAntiAltAsync(ctx.Guild.Id, minAgeMinutes, action, roleId: role.Id);

                await ctx.OkAsync();
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public Task AntiRaid()
            {
                if (_service.TryStopAntiRaid(ctx.Guild.Id))
                {
                    return ReplyConfirmLocalizedAsync("prot_disable", "Anti-Raid");
                }
                else
                {
                    return ReplyErrorLocalizedAsync("protection_not_running", "Anti-Raid");
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(1)]
            public Task AntiRaid(int userThreshold, int seconds,
                PunishmentAction action, [Leftover] StoopidTime punishTime)
                => InternalAntiRaid(userThreshold, seconds, action, punishTime: punishTime);

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(2)]
            public Task AntiRaid(int userThreshold, int seconds, PunishmentAction action)
                => InternalAntiRaid(userThreshold, seconds, action);

            private async Task InternalAntiRaid(int userThreshold, int seconds = 10,
                PunishmentAction action = PunishmentAction.Mute, StoopidTime punishTime = null)
            {
                if (action == PunishmentAction.AddRole)
                {
                    await ReplyErrorLocalizedAsync("punishment_unsupported", action);
                    return;
                }

                if (userThreshold < 2 || userThreshold > 30)
                {
                    await ReplyErrorLocalizedAsync("raid_cnt", 2, 30).ConfigureAwait(false);
                    return;
                }

                if (seconds < 2 || seconds > 300)
                {
                    await ReplyErrorLocalizedAsync("raid_time", 2, 300).ConfigureAwait(false);
                    return;
                }

                if (!(punishTime is null))
                {
                    if (!_service.IsDurationAllowed(action))
                    {
                        await ReplyErrorLocalizedAsync("prot_cant_use_time");
                    }
                }

                var time = (int?)punishTime?.Time.TotalMinutes ?? 0;
                if (time < 0 || time > 60 * 24)
                    return;

                var stats = await _service.StartAntiRaidAsync(ctx.Guild.Id, userThreshold, seconds,
                    action, time).ConfigureAwait(false);

                if (stats == null)
                {
                    return;
                }

                await ctx.Channel.SendConfirmAsync(GetText("prot_enable", "Anti-Raid"),
                        $"{ctx.User.Mention} {GetAntiRaidString(stats)}")
                        .ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public Task AntiSpam()
            {
                if (_service.TryStopAntiSpam(ctx.Guild.Id))
                {
                    return ReplyConfirmLocalizedAsync("prot_disable", "Anti-Spam");
                }
                else
                {
                    return ReplyErrorLocalizedAsync("protection_not_running", "Anti-Spam");
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(0)]
            public Task AntiSpam(int messageCount, PunishmentAction action, [Leftover] IRole role)
            {
                if (action != PunishmentAction.AddRole)
                    return Task.CompletedTask;

                return InternalAntiSpam(messageCount, action, null, role);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(1)]
            public Task AntiSpam(int messageCount, PunishmentAction action, [Leftover] StoopidTime punishTime)
                => InternalAntiSpam(messageCount, action, punishTime, null);

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(2)]
            public Task AntiSpam(int messageCount, PunishmentAction action)
                => InternalAntiSpam(messageCount, action);

            public async Task InternalAntiSpam(int messageCount, PunishmentAction action,
                StoopidTime timeData = null, IRole role = null)
            {
                if (messageCount < 2 || messageCount > 10)
                    return;

                if (!(timeData is null))
                {
                    if (!_service.IsDurationAllowed(action))
                    {
                        await ReplyErrorLocalizedAsync("prot_cant_use_time");
                    }
                }

                var time = (int?)timeData?.Time.TotalMinutes ?? 0;
                if (time < 0 || time > 60 * 24)
                    return;

                var stats = await _service.StartAntiSpamAsync(ctx.Guild.Id, messageCount, action, time, role?.Id).ConfigureAwait(false);

                await ctx.Channel.SendConfirmAsync(GetText("prot_enable", "Anti-Spam"),
                    $"{ctx.User.Mention} {GetAntiSpamString(stats)}").ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task AntispamIgnore()
            {
                var added = await _service.AntiSpamIgnoreAsync(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

                if (added is null)
                {
                    await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Spam").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalizedAsync(added.Value ? "spam_ignore" : "spam_not_ignore", "Anti-Spam").ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task AntiList()
            {
                var (spam, raid, alt) = _service.GetAntiStats(ctx.Guild.Id);

                if (spam is null && raid is null && alt is null)
                {
                    await ReplyConfirmLocalizedAsync("prot_none").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle(GetText("prot_active"));

                if (spam != null)
                    embed.AddField(efb => efb.WithName("Anti-Spam")
                        .WithValue(GetAntiSpamString(spam).TrimTo(1024))
                        .WithIsInline(true));

                if (raid != null)
                    embed.AddField(efb => efb.WithName("Anti-Raid")
                        .WithValue(GetAntiRaidString(raid).TrimTo(1024))
                        .WithIsInline(true));

                if (!(alt is null))
                    embed.AddField("Anti-Alt", GetAntiAltString(alt), true);

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            private string GetAntiAltString(AntiAltStats alt)
                => GetText("anti_alt_status",
                    Format.Bold(alt.MinAge.ToString(@"dd\d\ hh\h\ mm\m\ ")),
                    Format.Bold(alt.Action.ToString()),
                    Format.Bold(alt.Counter.ToString()));

            private string GetAntiSpamString(AntiSpamStats stats)
            {
                var settings = stats.AntiSpamSettings;
                var ignoredString = string.Join(", ", settings.IgnoredChannels.Select(c => $"<#{c.ChannelId}>"));

                if (string.IsNullOrWhiteSpace(ignoredString))
                    ignoredString = "none";

                string add = "";
                if (settings.MuteTime > 0)
                {
                    add = $" ({TimeSpan.FromMinutes(settings.MuteTime):hh\\hmm\\m})";
                }

                return GetText("spam_stats",
                        Format.Bold(settings.MessageThreshold.ToString()),
                        Format.Bold(settings.Action.ToString() + add),
                        ignoredString);
            }

            private string GetAntiRaidString(AntiRaidStats stats)
            {
                var actionString = Format.Bold(stats.AntiRaidSettings.Action.ToString());

                if (stats.AntiRaidSettings.PunishDuration > 0)
                {
                    actionString += $" **({TimeSpan.FromMinutes(stats.AntiRaidSettings.PunishDuration):hh\\hmm\\m})**";
                }

                return GetText("raid_stats",
                    Format.Bold(stats.AntiRaidSettings.UserThreshold.ToString()),
                    Format.Bold(stats.AntiRaidSettings.Seconds.ToString()),
                    actionString);
            }
        }
    }
}
