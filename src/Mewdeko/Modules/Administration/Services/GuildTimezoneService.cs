﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Extensions;

namespace Mewdeko.Modules.Administration.Services
{
    public class GuildTimezoneService : INService
    {
        private readonly DbService _db;
        private readonly ConcurrentDictionary<ulong, TimeZoneInfo> _timezones;

        public GuildTimezoneService(DiscordSocketClient client, Mewdeko bot, DbService db)
        {
            _timezones = bot.AllGuildConfigs
                .Select(GetTimzezoneTuple)
                .Where(x => x.Timezone != null)
                .ToDictionary(x => x.GuildId, x => x.Timezone)
                .ToConcurrent();

            var curUser = client.CurrentUser;
            if (curUser != null)
                AllServices.TryAdd(curUser.Id, this);
            _db = db;

            bot.JoinedGuild += Bot_JoinedGuild;
        }

        public static ConcurrentDictionary<ulong, GuildTimezoneService> AllServices { get; } = new();

        private Task Bot_JoinedGuild(GuildConfig arg)
        {
            var (guildId, tz) = GetTimzezoneTuple(arg);
            if (tz != null)
                _timezones.TryAdd(guildId, tz);
            return Task.CompletedTask;
        }

        private static (ulong GuildId, TimeZoneInfo Timezone) GetTimzezoneTuple(GuildConfig x)
        {
            TimeZoneInfo tz;
            try
            {
                if (x.TimeZoneId == null)
                    tz = null;
                else
                    tz = TimeZoneInfo.FindSystemTimeZoneById(x.TimeZoneId);
            }
            catch
            {
                tz = null;
            }

            return (x.GuildId, Timezone: tz);
        }

        public TimeZoneInfo GetTimeZoneOrDefault(ulong guildId)
        {
            if (_timezones.TryGetValue(guildId, out var tz))
                return tz;
            return null;
        }

        public void SetTimeZone(ulong guildId, TimeZoneInfo tz)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId, set => set);

                gc.TimeZoneId = tz?.Id;
                uow.SaveChanges();

                if (tz == null)
                    _timezones.TryRemove(guildId, out tz);
                else
                    _timezones.AddOrUpdate(guildId, tz, (key, old) => tz);
            }
        }

        public TimeZoneInfo GetTimeZoneOrUtc(ulong guildId)
        {
            return GetTimeZoneOrDefault(guildId) ?? TimeZoneInfo.Utc;
        }
    }
}