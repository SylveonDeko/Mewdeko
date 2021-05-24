using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Discord;
using Discord.WebSocket;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Core.Services.Impl;
using Mewdeko.Extensions;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Mewdeko.Modules.Utility.Services
{
    public class RemindService : INService
    {
        private readonly DiscordSocketClient _client;
        private readonly IBotConfigProvider _config;
        private readonly DbService _db;

        private readonly Logger _log;

        public RemindService(DiscordSocketClient client,
            IBotConfigProvider config,
            DbService db,
            StartingGuildsService guilds)
        {
            _config = config;
            _client = client;
            _log = LogManager.GetCurrentClassLogger();
            _db = db;

            List<Reminder> reminders;
            using (var uow = _db.GetDbContext())
            {
                reminders = uow.Reminders.GetIncludedReminders(guilds).ToList();
            }

            RemindMessageFormat = _config.BotConfig.RemindMessageFormat;

            foreach (var r in reminders) StartReminder(r);
        }

        public Regex Regex { get; } =
            new(
                @"^(?:(?<months>\d)mo)?(?:(?<weeks>\d)w)?(?:(?<days>\d{1,2})d)?(?:(?<hours>\d{1,2})h)?(?:(?<minutes>\d{1,2})m)?$"
                ,
                RegexOptions.Compiled | RegexOptions.Multiline);

        public string RemindMessageFormat { get; }

        public ConcurrentDictionary<int, Timer> Reminders { get; } = new();

        public void StartReminder(Reminder r)
        {
            var time = r.When - DateTime.UtcNow;

            if (time.TotalMilliseconds > int.MaxValue)
                return;

            if (time.TotalMilliseconds < 0)
                time = TimeSpan.FromSeconds(5);

            var remT = new Timer(ReminderTimerAction, r, (int) time.TotalMilliseconds, Timeout.Infinite);
            if (!Reminders.TryAdd(r.Id, remT)) remT.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async void ReminderTimerAction(object rObj)
        {
            var r = (Reminder) rObj;

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
                        .AddField("By",
                            (await ch.GetUserAsync(r.UserId).ConfigureAwait(false))?.ToString() ?? r.UserId.ToString()),
                    r.Message.SanitizeMentions()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Info(ex.Message + $"({r.Id})");
            }
            finally
            {
                using (var uow = _db.GetDbContext())
                {
                    uow._context.Database.ExecuteSqlInterpolated($"DELETE FROM Reminders WHERE Id={r.Id};");
                    uow.SaveChanges();
                }

                RemoveReminder(r.Id);
            }
        }

        public void RemoveReminder(int id)
        {
            if (Reminders.TryRemove(id, out var t)) t.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}