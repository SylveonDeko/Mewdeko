using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Mewdeko._Extensions;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Moderation.Services
{
    public class UserPunishService2 : INService
    {
        private readonly DbService _db;
        private readonly MuteService _mute;
        private readonly Timer _warnExpiryTimer;
        private Mewdeko.Services.Mewdeko _bot;


        public UserPunishService2(MuteService mute, DbService db, Mewdeko.Services.Mewdeko bot)
        {
            _mute = mute;
            _db = db;
            _bot = bot;
            _miniwarnlogchannelids = bot.AllGuildConfigs
                .Where(x => x.MiniWarnlogChannelId != 0)
                .ToDictionary(x => x.GuildId, x => x.MiniWarnlogChannelId)
                .ToConcurrent();


            _warnExpiryTimer = new Timer(async _ => { await CheckAllWarnExpiresAsync(); }, null,
                TimeSpan.FromSeconds(0), TimeSpan.FromHours(12));
        }

        private ConcurrentDictionary<ulong, ulong> _miniwarnlogchannelids { get; } = new();

        public ulong GetMWarnlogChannel(ulong? id)
        {
            if (id == null || !_miniwarnlogchannelids.TryGetValue(id.Value, out var mwarnlogchannel))
                return 0;

            return mwarnlogchannel;
        }

        public async Task SetMWarnlogChannelId(IGuild guild, ITextChannel channel)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.MiniWarnlogChannelId = channel.Id;
                await uow.SaveChangesAsync();
            }

            _miniwarnlogchannelids.AddOrUpdate(guild.Id, channel.Id, (key, old) => channel.Id);
        }

        public async Task<WarningPunishment2> Warn(IGuild guild, ulong userId, IUser mod, string reason)
        {
            var modName = mod.ToString();

            if (string.IsNullOrWhiteSpace(reason))
                reason = "-";

            var guildId = guild.Id;

            var warn2 = new Warning2
            {
                UserId = userId,
                GuildId = guildId,
                Forgiven = false,
                Reason = reason,
                Moderator = modName
            };

            var warnings = 1;
            List<WarningPunishment2> ps;
            using (var uow = _db.GetDbContext())
            {
                ps = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.WarnPunishments2))
                    .WarnPunishments2;

                warnings += uow.Warnings2
                    .ForId(guildId, userId)
                    .Count(w => !w.Forgiven && w.UserId == userId);

                uow.Warnings2.Add(warn2);

                await uow.SaveChangesAsync();
            }

            var p = ps.FirstOrDefault(x => x.Count == warnings);

            if (p != null)
            {
                var user = await guild.GetUserAsync(userId).ConfigureAwait(false);
                if (user == null)
                    return null;
                switch (p.Punishment)
                {
                    case PunishmentAction.Mute:
                        if (p.Time == 0)
                            await _mute.MuteUser(user, mod).ConfigureAwait(false);
                        else
                            await _mute.TimedMute(user, mod, TimeSpan.FromMinutes(p.Time)).ConfigureAwait(false);
                        break;
                    case PunishmentAction.VoiceMute:
                        if (p.Time == 0)
                            await _mute.MuteUser(user, mod, MuteType.Voice).ConfigureAwait(false);
                        else
                            await _mute.TimedMute(user, mod, TimeSpan.FromMinutes(p.Time), MuteType.Voice)
                                .ConfigureAwait(false);
                        break;
                    case PunishmentAction.ChatMute:
                        if (p.Time == 0)
                            await _mute.MuteUser(user, mod, MuteType.Chat).ConfigureAwait(false);
                        else
                            await _mute.TimedMute(user, mod, TimeSpan.FromMinutes(p.Time), MuteType.Chat)
                                .ConfigureAwait(false);
                        break;
                    case PunishmentAction.Kick:
                        await user.KickAsync("Warned too many times.").ConfigureAwait(false);
                        break;
                    case PunishmentAction.Ban:
                        if (p.Time == 0)
                            await guild.AddBanAsync(user, reason: "Warned too many times.").ConfigureAwait(false);
                        else
                            await _mute.TimedBan(guild, user, TimeSpan.FromMinutes(p.Time), "Warned too many times.")
                                .ConfigureAwait(false);
                        break;
                    case PunishmentAction.Softban:
                        await guild.AddBanAsync(user, 7, "Warned too many times").ConfigureAwait(false);
                        try
                        {
                            await guild.RemoveBanAsync(user).ConfigureAwait(false);
                        }
                        catch
                        {
                            await guild.RemoveBanAsync(user).ConfigureAwait(false);
                        }

                        break;
                    case PunishmentAction.RemoveRoles:
                        await user.RemoveRolesAsync(user.GetRoles().Where(x => x.Id != guild.EveryoneRole.Id))
                            .ConfigureAwait(false);
                        break;
                    case PunishmentAction.AddRole:
                        var role = guild.GetRole(p.RoleId.Value);
                        if (!(role is null))
                        {
                            if (p.Time == 0)
                                await user.AddRoleAsync(role).ConfigureAwait(false);
                            else
                                await _mute.TimedRole(user, TimeSpan.FromMinutes(p.Time), "Warned too many times.",
                                    role).ConfigureAwait(false);
                        }
                        else
                        {
                            Log.Warning($"Warnpunish can't find role {p.RoleId.Value} on server {guild.Id}");
                        }

                        break;
                }

                return p;
            }

            return null;
        }

        public int GetWarnings(IGuild guild, ulong userId)
        {
            int warnings;
            using (var uow = _db.GetDbContext())
            {
                warnings = uow.Warnings2
                    .ForId(guild.Id, userId)
                    .Count(w => !w.Forgiven && w.UserId == userId);
            }

            return warnings;
        }

        public async Task CheckAllWarnExpiresAsync()
        {
            using (var uow = _db.GetDbContext())
            {
                var cleared = await uow._context.Database.ExecuteSqlRawAsync(@"UPDATE Warnings2
SET Forgiven = 1,
    ForgivenBy = 'Expiry'
WHERE GuildId in (SELECT GuildId FROM GuildConfigs WHERE WarnExpireHours > 0 AND WarnExpireAction = 0)
	AND Forgiven = 0
	AND DateAdded < datetime('now', (SELECT '-' || WarnExpireHours || ' hours' FROM GuildConfigs as gc WHERE gc.GuildId = Warnings2.GuildId));");

                var deleted = await uow._context.Database.ExecuteSqlRawAsync(@"DELETE FROM Warnings2
WHERE GuildId in (SELECT GuildId FROM GuildConfigs WHERE WarnExpireHours > 0 AND WarnExpireAction = 1)
	AND DateAdded < datetime('now', (SELECT '-' || WarnExpireHours || ' hours' FROM GuildConfigs as gc WHERE gc.GuildId = Warnings2.GuildId));");

                if (cleared > 0 || deleted > 0)
                    Log.Information($"Cleared {cleared} warnings and deleted {deleted} warnings due to expiry.");
            }
        }

        public async Task CheckWarnExpiresAsync(ulong guildId)
        {
            using (var uow = _db.GetDbContext())
            {
                var config = uow.GuildConfigs.ForId(guildId, inc => inc);

                if (config.WarnExpireHours == 0)
                    return;

                var hours = $"{-config.WarnExpireHours} hours";
                if (config.WarnExpireAction == WarnExpireAction.Clear)
                    await uow._context.Database.ExecuteSqlInterpolatedAsync($@"UPDATE warnings
SET Forgiven = 1,
    ForgivenBy = 'Expiry'
WHERE GuildId={guildId}
    AND Forgiven = 0
    AND DateAdded < datetime('now', {hours})");
                else if (config.WarnExpireAction == WarnExpireAction.Delete)
                    await uow._context.Database.ExecuteSqlInterpolatedAsync($@"DELETE FROM warnings
WHERE GuildId={guildId}
    AND DateAdded < datetime('now', {hours})");

                await uow.SaveChangesAsync();
            }
        }

        public async Task WarnExpireAsync(ulong guildId, int days, bool delete)
        {
            using (var uow = _db.GetDbContext())
            {
                var config = uow.GuildConfigs.ForId(guildId, inc => inc);

                config.WarnExpireHours = days * 24;
                config.WarnExpireAction = delete ? WarnExpireAction.Delete : WarnExpireAction.Clear;
                await uow.SaveChangesAsync();

                // no need to check for warn expires
                if (config.WarnExpireHours == 0)
                    return;
            }

            await CheckWarnExpiresAsync(guildId);
        }

        public IGrouping<ulong, Warning2>[] WarnlogAll(ulong gid)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.Warnings2.GetForGuild(gid).GroupBy(x => x.UserId).ToArray();
            }
        }

        public Warning2[] UserWarnings(ulong gid, ulong userId)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.Warnings2.ForId(gid, userId);
            }
        }

        public async Task<bool> WarnClearAsync(ulong guildId, ulong userId, int index, string moderator)
        {
            var toReturn = true;
            using (var uow = _db.GetDbContext())
            {
                if (index == 0)
                    await uow.Warnings2.ForgiveAll(guildId, userId, moderator);
                else
                    toReturn = uow.Warnings2.Forgive(guildId, userId, moderator, index - 1);
                await uow.SaveChangesAsync();
            }

            return toReturn;
        }

        public bool WarnPunish(ulong guildId, int number, PunishmentAction punish, StoopidTime time, IRole role = null)
        {
            // these 3 don't make sense with time
            if ((punish == PunishmentAction.Softban || punish == PunishmentAction.Kick ||
                 punish == PunishmentAction.RemoveRoles) && time != null)
                return false;
            if (number <= 0 || time != null && time.Time > TimeSpan.FromDays(49))
                return false;

            using (var uow = _db.GetDbContext())
            {
                var ps = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.WarnPunishments2)).WarnPunishments2;
                var toDelete = ps.Where(x => x.Count == number);

                uow._context.RemoveRange(toDelete);

                ps.Add(new WarningPunishment2
                {
                    Count = number,
                    Punishment = punish,
                    Time = (int?)time?.Time.TotalMinutes ?? 0,
                    RoleId = punish == PunishmentAction.AddRole ? role.Id : default(ulong?)
                });
                uow.SaveChanges();
            }

            return true;
        }

        public bool WarnPunishRemove(ulong guildId, int number)
        {
            if (number <= 0)
                return false;

            using (var uow = _db.GetDbContext())
            {
                var ps = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.WarnPunishments2)).WarnPunishments2;
                var p = ps.FirstOrDefault(x => x.Count == number);

                if (p != null)
                {
                    uow._context.Remove(p);
                    uow.SaveChanges();
                }
            }

            return true;
        }

        public WarningPunishment2[] WarnPunishList(ulong guildId)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.GuildConfigs.ForId(guildId, gc => gc.Include(x => x.WarnPunishments2))
                    .WarnPunishments2
                    .OrderBy(x => x.Count)
                    .ToArray();
            }
        }
    }
}