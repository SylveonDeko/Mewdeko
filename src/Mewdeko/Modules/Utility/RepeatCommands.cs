using System.Collections.Concurrent;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    [Group]
    public class RepeatCommands : MewdekoSubmodule<MessageRepeaterService>
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;

        public RepeatCommands(DiscordSocketClient client, DbService db)
        {
            _client = client;
            _db = db;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatInvoke(int index)
        {
            if (!Service.RepeaterReady)
                return;
            index -= 1;
            if (!Service.Repeaters.TryGetValue(ctx.Guild.Id, out var rep))
            {
                await ReplyErrorLocalizedAsync("repeat_invoke_none").ConfigureAwait(false);
                return;
            }

            var repList = rep.ToList();

            if (index >= repList.Count)
            {
                await ReplyErrorLocalizedAsync("index_out_of_range").ConfigureAwait(false);
                return;
            }

            var repeater = repList[index];
            repeater.Value.Reset();
            await repeater.Value.Trigger().ConfigureAwait(false);

            try
            {
                await ctx.Message.AddReactionAsync(new Emoji("🔄")).ConfigureAwait(false);
            }
            catch
            {
                // excluded
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatRemove(int index)
        {
            if (!Service.RepeaterReady)
                return;
            if (--index < 0)
                return;

            if (!Service.Repeaters.TryGetValue(ctx.Guild.Id, out var guildRepeaters))
                return;

            var repeaterList = guildRepeaters.ToList();

            if (index >= repeaterList.Count)
            {
                await ReplyErrorLocalizedAsync("index_out_of_range").ConfigureAwait(false);
                return;
            }

            var (_, value) = repeaterList[index];

            // wat
            if (!guildRepeaters.TryRemove(value.Repeater.Id, out var runner))
                return;

            // take description before stopping just in case
            var description = GetRepeaterInfoString(runner);
            runner.Stop();

            using (var uow = _db.GetDbContext())
            {
                var guildConfig = uow.GuildConfigs.ForId(ctx.Guild.Id, set => set.Include(gc => gc.GuildRepeaters));

                var item = guildConfig.GuildRepeaters.FirstOrDefault(r => r.Id == value.Repeater.Id);
                if (item != null)
                {
                    guildConfig.GuildRepeaters.Remove(item);
                    uow._context.Remove(item);
                }

                await uow.SaveChangesAsync();
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle(GetText("repeater_removed", index + 1))
                .WithDescription(description));
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatRedundant(int index)
        {
            if (!Service.RepeaterReady)
                return;

            if (!Service.Repeaters.TryGetValue(ctx.Guild.Id, out var guildRepeaters))
                return;

            var repeaterList = guildRepeaters.ToList();

            if (--index < 0 || index >= repeaterList.Count)
            {
                await ReplyErrorLocalizedAsync("index_out_of_range").ConfigureAwait(false);
                return;
            }

            var repeater = repeaterList[index].Value.Repeater;
            var newValue = repeater.NoRedundant = !repeater.NoRedundant;
            using (var uow = _db.GetDbContext())
            {
                var guildConfig = uow.GuildConfigs.ForId(ctx.Guild.Id, set => set.Include(gc => gc.GuildRepeaters));

                var item = guildConfig.GuildRepeaters.FirstOrDefault(r => r.Id == repeater.Id);
                if (item != null) item.NoRedundant = newValue;

                await uow.SaveChangesAsync();
            }

            if (newValue)
                await ReplyConfirmLocalizedAsync("repeater_redundant_no", index + 1);
            else
                await ReplyConfirmLocalizedAsync("repeater_redundant_yes", index + 1);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(-1)]
        public Task Repeat([Remainder] string message) => Repeat(null, null, message);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(0)]
        public Task Repeat(StoopidTime interval, [Remainder] string message) => Repeat(null, interval, message);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(1)]
        public Task Repeat(GuildDateTime dt, [Remainder] string message) => Repeat(dt, null, message);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(2)]
        public async Task Repeat(GuildDateTime dt, StoopidTime interval, [Remainder] string message)
        {
            if (!Service.RepeaterReady)
                return;

            var startTimeOfDay = dt?.InputTimeUtc.TimeOfDay;
            // if interval not null, that means user specified it (don't change it)

            // if interval is null set the default to:
            // if time of day is specified: 1 day
            // else 5 minutes
            var realInterval = interval?.Time ?? (startTimeOfDay is null
                ? TimeSpan.FromMinutes(5)
                : TimeSpan.FromDays(1));

            if (string.IsNullOrWhiteSpace(message)
                || interval != null &&
                (interval.Time > TimeSpan.FromMinutes(25000) || interval.Time < TimeSpan.FromMinutes(1)))
                return;

            var toAdd = new Repeater
            {
                ChannelId = ctx.Channel.Id,
                GuildId = ctx.Guild.Id,
                Interval = realInterval,
                Message = ((IGuildUser) ctx.User).GuildPermissions.MentionEveryone
                    ? message
                    : message.SanitizeMentions(true),
                NoRedundant = false,
                StartTimeOfDay = startTimeOfDay
            };

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(ctx.Guild.Id, set => set.Include(x => x.GuildRepeaters));
                gc.GuildRepeaters.Add(toAdd);

                await uow.SaveChangesAsync();
            }

            var runner = new RepeatRunner(_client, (SocketGuild) ctx.Guild, toAdd, Service);

            Service.Repeaters.AddOrUpdate(ctx.Guild.Id,
                new ConcurrentDictionary<int, RepeatRunner>(new[]
                    {new KeyValuePair<int, RepeatRunner>(toAdd.Id, runner)}), (key, old) =>
                {
                    old.TryAdd(runner.Repeater.Id, runner);
                    return old;
                });

            var description = GetRepeaterInfoString(runner);
            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle(GetText("repeater_created"))
                .WithDescription(description));
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatList()
        {
            if (!Service.RepeaterReady)
                return;
            if (!Service.Repeaters.TryGetValue(ctx.Guild.Id, out var repRunners))
            {
                await ReplyConfirmLocalizedAsync("repeaters_none").ConfigureAwait(false);
                return;
            }

            var replist = repRunners.ToList();

            var embed = new EmbedBuilder()
                .WithTitle(GetText("list_of_repeaters"))
                .WithOkColor();

            if (replist.Count == 0) embed.WithDescription(GetText("no_active_repeaters"));

            for (var i = 0; i < replist.Count; i++)
            {
                var (_, runner) = replist[i];

                var description = GetRepeaterInfoString(runner);
                var name = $"#{Format.Code((i + 1).ToString())}";
                embed.AddField(
                    name,
                    description
                );
            }

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private string GetRepeaterInfoString(RepeatRunner runner)
        {
            var intervalString = Format.Bold(runner.Repeater.Interval.ToPrettyStringHm());
            var executesIn = runner.NextDateTime - DateTime.UtcNow;
            var executesInString = Format.Bold(executesIn.ToPrettyStringHm());
            var message = Format.Sanitize(runner.Repeater.Message.TrimTo(50));

            var description = "";
            if (runner.Repeater.NoRedundant)
                description = Format.Underline(Format.Bold(GetText("no_redundant:"))) + "\n\n";

            description += $"<#{runner.Repeater.ChannelId}>\n" +
                           $"`{GetText("interval:")}` {intervalString}\n" +
                           $"`{GetText("executes_in:")}` {executesInString}\n" +
                           $"`{GetText("message:")}` {message}";

            return description;
        }
    }
}