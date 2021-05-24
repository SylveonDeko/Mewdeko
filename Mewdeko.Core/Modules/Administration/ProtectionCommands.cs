using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class ProtectionCommands : MewdekoSubmodule<ProtectionService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public Task AntiRaid()
            {
                if (_service.TryStopAntiRaid(ctx.Guild.Id))
                    return ReplyConfirmLocalizedAsync("prot_disable", "Anti-Raid");
                return ReplyErrorLocalizedAsync("anti_raid_not_running");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task AntiRaid(int userThreshold, int seconds = 10,
                PunishmentAction action = PunishmentAction.Mute)
            {
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

                var stats = await _service.StartAntiRaidAsync(ctx.Guild.Id, userThreshold, seconds, action)
                    .ConfigureAwait(false);

                await ctx.Channel.SendConfirmAsync(GetText("prot_enable", "Anti-Raid"),
                        $"{ctx.User.Mention} {GetAntiRaidString(stats)}")
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(1)]
            public Task AntiSpam()
            {
                if (_service.TryStopAntiSpam(ctx.Guild.Id))
                    return ReplyConfirmLocalizedAsync("prot_disable", "Anti-Spam");
                return ReplyErrorLocalizedAsync("anti_spam_not_running");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(0)]
            public async Task AntiSpam(int messageCount, PunishmentAction action = PunishmentAction.Mute, int time = 0)
            {
                if (messageCount < 2 || messageCount > 10)
                    return;

                if (time < 0 || time > 60 * 60 * 12)
                    return;

                var stats = await _service.StartAntiSpamAsync(ctx.Guild.Id, messageCount, time, action)
                    .ConfigureAwait(false);

                await ctx.Channel.SendConfirmAsync(GetText("prot_enable", "Anti-Spam"),
                    $"{ctx.User.Mention} {GetAntiSpamString(stats)}").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task AntispamIgnore()
            {
                var added = await _service.AntiSpamIgnoreAsync(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

                if (added is null)
                {
                    await ReplyErrorLocalizedAsync("anti_spam_not_running").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalizedAsync(added.Value ? "spam_ignore" : "spam_not_ignore", "Anti-Spam")
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task AntiList()
            {
                var (spam, raid) = _service.GetAntiStats(ctx.Guild.Id);

                if (spam == null && raid == null)
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

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }


            private string GetAntiSpamString(AntiSpamStats stats)
            {
                var settings = stats.AntiSpamSettings;
                var ignoredString = string.Join(", ", settings.IgnoredChannels.Select(c => $"<#{c.ChannelId}>"));

                if (string.IsNullOrWhiteSpace(ignoredString))
                    ignoredString = "none";

                var add = "";
                if (settings.Action == PunishmentAction.Mute
                    && settings.MuteTime > 0)
                    add = " (" + settings.MuteTime + "s)";

                return GetText("spam_stats",
                    Format.Bold(settings.MessageThreshold.ToString()),
                    Format.Bold(settings.Action + add),
                    ignoredString);
            }

            private string GetAntiRaidString(AntiRaidStats stats)
            {
                return GetText("raid_stats",
                    Format.Bold(stats.AntiRaidSettings.UserThreshold.ToString()),
                    Format.Bold(stats.AntiRaidSettings.Seconds.ToString()),
                    Format.Bold(stats.AntiRaidSettings.Action.ToString()));
            }
        }
    }
}