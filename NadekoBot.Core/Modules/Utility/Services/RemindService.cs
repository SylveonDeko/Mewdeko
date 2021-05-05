using System;
using System.Linq;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NLog;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace NadekoBot.Modules.Utility.Services
{
    public class RemindService : INService
    {
        private readonly Regex _regex = new Regex(@"^(?:in\s?)?\s*(?:(?<mo>\d+)(?:\s?(?:months?|mos?),?))?(?:(?:\sand\s|\s*)?(?<w>\d+)(?:\s?(?:weeks?|w),?))?(?:(?:\sand\s|\s*)?(?<d>\d+)(?:\s?(?:days?|d),?))?(?:(?:\sand\s|\s*)?(?<h>\d+)(?:\s?(?:hours?|h),?))?(?:(?:\sand\s|\s*)?(?<m>\d+)(?:\s?(?:minutes?|mins?|m),?))?\s+(?:to:?\s+)?(?<what>(?:\r\n|[\r\n]|.)+)",
                                RegexOptions.Compiled | RegexOptions.Multiline);

        public string RemindMessageFormat { get; }

        private readonly Logger _log;
        private readonly IBotConfigProvider _config;
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        private readonly IBotCredentials _creds;

        public RemindService(DiscordSocketClient client,
            IBotConfigProvider config,
            DbService db,
            IBotCredentials creds)
        {
            _config = config;
            _client = client;
            _log = LogManager.GetCurrentClassLogger();
            _db = db;
            _creds = creds;

            RemindMessageFormat = _config.BotConfig.RemindMessageFormat;
            _ = StartReminderLoop();
        }

        private async Task StartReminderLoop()
        {
            while (true)
            {
                await Task.Delay(15000);
                try
                {
                    var now = DateTime.UtcNow;
                    var reminders = await GetRemindersBeforeAsync(now);
                    if (reminders.Count == 0)
                        continue;
                    
                    _log.Info($"Executing {reminders.Count} reminders.");
                    
                    // make groups of 5, with 1.5 second inbetween each one to ensure against ratelimits
                    var i = 0;
                    foreach (var group in reminders
                        .GroupBy(_ => ++i / (reminders.Count / 5 + 1)))
                    {
                        var executedReminders = group.ToList();
                        await Task.WhenAll(executedReminders.Select(ReminderTimerAction));
                        await RemoveReminders(executedReminders);
                        await Task.Delay(1500); 
                    }
                }
                catch (Exception ex)
                {
                    _log.Warn($"Error in reminder loop: {ex.Message}");
                    _log.Warn(ex.ToString());
                }
            }
        }

        private async Task RemoveReminders(List<Reminder> reminders)
        {
            using (var uow = _db.GetDbContext())
            {
                uow._context.Set<Reminder>()
                    .RemoveRange(reminders);

                await uow.SaveChangesAsync();
            }
        }

        private Task<List<Reminder>> GetRemindersBeforeAsync(DateTime now)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow._context.Reminders
                    .FromSqlInterpolated($"select * from reminders where ((serverid >> 22) % {_creds.TotalShards}) == {_client.ShardId} and \"when\" < {now};")
                    .ToListAsync();
            }
        }

        public struct RemindObject
        {
            public string What { get; set; }
            public TimeSpan Time { get; set; }
        }

        public bool TryParseRemindMessage(string input, out RemindObject obj)
        {
            var m = _regex.Match(input);

            obj = default;
            if (m.Length == 0)
            {
                return false;
            }
            
            var values = new Dictionary<string, int>();
            
            var what = m.Groups["what"].Value;

            if (string.IsNullOrWhiteSpace(what))
            {
                _log.Warn("No message provided for the reminder.");
                return false;
            }
            
            foreach (var groupName in _regex.GetGroupNames())
            {
                if (groupName == "0" || groupName== "what") continue;
                if (string.IsNullOrWhiteSpace(m.Groups[groupName].Value))
                {
                    values[groupName] = 0;
                    continue;
                }
                if (!int.TryParse(m.Groups[groupName].Value, out var value))
                {
                    _log.Warn($"Reminder regex group {groupName} has invalid value.");
                    return false;
                }

                if (value < 1)
                {
                    _log.Warn("Reminder time value has to be an integer greater than 0.");
                    return false;
                }
                
                values[groupName] = value;
            }
            
            var ts = new TimeSpan
            (
            30 * values["mo"] + 7 * values["w"] + values["d"],
                values["h"],
                values["m"],
                0
            );

            obj = new RemindObject()
            {
                Time = ts,
                What = what
            };

            return true;
        }

        private async Task ReminderTimerAction(Reminder r)
        {
            try
            {
                IMessageChannel ch;
                if (r.IsPrivate)
                {
                    var user = _client.GetUser(r.ChannelId);
                    if (user == null)
                        return;
                    ch = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                }
                else
                {
                    ch = _client.GetGuild(r.ServerId)?.GetTextChannel(r.ChannelId);
                }
                if (ch == null)
                    return;

                await ch.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Reminder")
                    .AddField("Created At", r.DateAdded.HasValue ? r.DateAdded.Value.ToLongDateString() : "?")
                    .AddField("By", (await ch.GetUserAsync(r.UserId).ConfigureAwait(false))?.ToString() ?? r.UserId.ToString()),
                    msg: r.Message).ConfigureAwait(false);
            }
            catch (Exception ex) { _log.Info(ex.Message + $"({r.Id})"); }
        }
    }
}