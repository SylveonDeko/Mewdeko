using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Replacements;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Moderation.Services
{
    public class UserPunishService : INService
    {
        private readonly BlacklistService _blacklistService;
        private readonly Mewdeko _bot;
        private readonly DbService _db;
        private readonly MuteService _mute;
        private readonly Timer _warnExpiryTimer;

        public UserPunishService(MuteService mute, DbService db, BlacklistService blacklistService, Mewdeko bot)
        {
            _warnlogchannelids = bot.AllGuildConfigs
                .Where(x => x.WarnlogChannelId != 0)
                .ToDictionary(x => x.GuildId, x => x.WarnlogChannelId)
                .ToConcurrent();
            _mute = mute;
            _bot = bot;
            _db = db;
            _blacklistService = blacklistService;

            _warnExpiryTimer = new Timer(async _ => { await CheckAllWarnExpiresAsync(); }, null,
                TimeSpan.FromSeconds(0), TimeSpan.FromHours(12));
        }

        private ConcurrentDictionary<ulong, ulong> _warnlogchannelids { get; } = new();

        public ulong GetWarnlogChannel(ulong? id)
        {
            if (id == null || !_warnlogchannelids.TryGetValue(id.Value, out var warnlogchannel))
                return 0;

            return warnlogchannel;
        }

        public async Task SetWarnlogChannelId(IGuild guild, ITextChannel channel)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.WarnlogChannelId = channel.Id;
                await uow.SaveChangesAsync();
            }

            _warnlogchannelids.AddOrUpdate(guild.Id, channel.Id, (key, old) => channel.Id);
        }

        public async Task<WarningPunishment> Warn(IGuild guild, ulong userId, IUser mod, string reason)
        {
            var modName = mod.ToString();

            if (string.IsNullOrWhiteSpace(reason))
                reason = "-";

            var guildId = guild.Id;

            var warn = new Warning
            {
                UserId = userId,
                GuildId = guildId,
                Forgiven = false,
                Reason = reason,
                Moderator = modName
            };

            var warnings = 1;
            List<WarningPunishment> ps;
            using (var uow = _db.GetDbContext())
            {
                ps = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.WarnPunishments))
                    .WarnPunishments;

                warnings += uow.Warnings
                    .ForId(guildId, userId)
                    .Count(w => !w.Forgiven && w.UserId == userId);

                uow.Warnings.Add(warn);

                uow.SaveChanges();
            }

            var p = ps.FirstOrDefault(x => x.Count == warnings);

            if (p != null)
            {
                var user = await guild.GetUserAsync(userId).ConfigureAwait(false);
                if (user == null)
                    return null;

                await ApplyPunishment(guild, user, mod, p.Punishment, p.Time, p.RoleId, "Warned too many times.");
                return p;
            }

            return null;
        }

        public async Task ApplyPunishment(IGuild guild, IGuildUser user, IUser mod, PunishmentAction p, int minutes,
            ulong? roleId, string reason)
        {
            switch (p)
            {
                case PunishmentAction.Mute:
                    if (minutes == 0)
                        await _mute.MuteUser(user, mod, reason: reason).ConfigureAwait(false);
                    else
                        await _mute.TimedMute(user, mod, TimeSpan.FromMinutes(minutes), reason: reason)
                            .ConfigureAwait(false);
                    break;
                case PunishmentAction.VoiceMute:
                    if (minutes == 0)
                        await _mute.MuteUser(user, mod, MuteType.Voice, reason).ConfigureAwait(false);
                    else
                        await _mute.TimedMute(user, mod, TimeSpan.FromMinutes(minutes), MuteType.Voice, reason)
                            .ConfigureAwait(false);
                    break;
                case PunishmentAction.ChatMute:
                    if (minutes == 0)
                        await _mute.MuteUser(user, mod, MuteType.Chat, reason).ConfigureAwait(false);
                    else
                        await _mute.TimedMute(user, mod, TimeSpan.FromMinutes(minutes), MuteType.Chat, reason)
                            .ConfigureAwait(false);
                    break;
                case PunishmentAction.Kick:
                    await user.KickAsync(reason).ConfigureAwait(false);
                    break;
                case PunishmentAction.Ban:
                    if (minutes == 0)
                        await guild.AddBanAsync(user, reason: reason).ConfigureAwait(false);
                    else
                        await _mute.TimedBan(user.Guild, user, TimeSpan.FromMinutes(minutes), reason)
                            .ConfigureAwait(false);
                    break;
                case PunishmentAction.Softban:
                    await guild.AddBanAsync(user, 7, $"Softban | {reason}").ConfigureAwait(false);
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
                    await user.RemoveRolesAsync(user.GetRoles().Where(x => !x.IsManaged && x != x.Guild.EveryoneRole))
                        .ConfigureAwait(false);
                    break;
                case PunishmentAction.AddRole:
                    if (roleId is null)
                        return;
                    var role = guild.GetRole(roleId.Value);
                    if (!(role is null))
                    {
                        if (minutes == 0)
                            await user.AddRoleAsync(role).ConfigureAwait(false);
                        else
                            await _mute.TimedRole(user, TimeSpan.FromMinutes(minutes), reason, role)
                                .ConfigureAwait(false);
                    }
                    else
                    {
                        Log.Warning($"Can't find role {roleId.Value} on server {guild.Id} to apply punishment.");
                    }

                    break;
            }
        }

        public async Task CheckAllWarnExpiresAsync()
        {
            using (var uow = _db.GetDbContext())
            {
                var cleared = await uow._context.Database.ExecuteSqlRawAsync(@"UPDATE Warnings
SET Forgiven = 1,
    ForgivenBy = 'Expiry'
WHERE GuildId in (SELECT GuildId FROM GuildConfigs WHERE WarnExpireHours > 0 AND WarnExpireAction = 0)
	AND Forgiven = 0
	AND DateAdded < datetime('now', (SELECT '-' || WarnExpireHours || ' hours' FROM GuildConfigs as gc WHERE gc.GuildId = Warnings.GuildId));");

                var deleted = await uow._context.Database.ExecuteSqlRawAsync(@"DELETE FROM Warnings
WHERE GuildId in (SELECT GuildId FROM GuildConfigs WHERE WarnExpireHours > 0 AND WarnExpireAction = 1)
	AND DateAdded < datetime('now', (SELECT '-' || WarnExpireHours || ' hours' FROM GuildConfigs as gc WHERE gc.GuildId = Warnings.GuildId));");

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

        public IGrouping<ulong, Warning>[] WarnlogAll(ulong gid)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.Warnings.GetForGuild(gid).GroupBy(x => x.UserId).ToArray();
            }
        }

        public Warning[] UserWarnings(ulong gid, ulong userId)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.Warnings.ForId(gid, userId);
            }
        }

        public async Task<bool> WarnClearAsync(ulong guildId, ulong userId, int index, string moderator)
        {
            var toReturn = true;
            using (var uow = _db.GetDbContext())
            {
                if (index == 0)
                    await uow.Warnings.ForgiveAll(guildId, userId, moderator);
                else
                    toReturn = uow.Warnings.Forgive(guildId, userId, moderator, index - 1);
                uow.SaveChanges();
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
                var ps = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.WarnPunishments)).WarnPunishments;
                var toDelete = ps.Where(x => x.Count == number);

                uow._context.RemoveRange(toDelete);

                ps.Add(new WarningPunishment
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
                var ps = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.WarnPunishments)).WarnPunishments;
                var p = ps.FirstOrDefault(x => x.Count == number);

                if (p != null)
                {
                    uow._context.Remove(p);
                    uow.SaveChanges();
                }
            }

            return true;
        }

        public WarningPunishment[] WarnPunishList(ulong guildId)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow.GuildConfigs.ForId(guildId, gc => gc.Include(x => x.WarnPunishments))
                    .WarnPunishments
                    .OrderBy(x => x.Count)
                    .ToArray();
            }
        }

        public (IEnumerable<(string Original, ulong? Id, string Reason)> Bans, int Missing) MassKill(SocketGuild guild,
            string people)
        {
            var gusers = guild.Users;
            //get user objects and reasons
            var bans = people.Split("\n")
                .Select(x =>
                {
                    var split = x.Trim().Split(" ");

                    var reason = string.Join(" ", split.Skip(1));

                    if (ulong.TryParse(split[0], out var id))
                        return (Original: split[0], Id: id, Reason: reason);

                    return (Original: split[0],
                        gusers
                            .FirstOrDefault(u => u.ToString().ToLowerInvariant() == x)
                            ?.Id,
                        Reason: reason);
                })
                .ToArray();

            //if user is null, means that person couldn't be found
            var missing = bans
                .Count(x => !x.Id.HasValue);

            //get only data for found users
            var found = bans
                .Where(x => x.Id.HasValue)
                .Select(x => x.Id.Value)
                .ToList();

            _blacklistService.BlacklistUsers(found);

            return (bans, missing);
        }

        public string GetBanTemplate(ulong guildId)
        {
            using (var uow = _db.GetDbContext())
            {
                var template = uow._context.BanTemplates
                    .AsQueryable()
                    .FirstOrDefault(x => x.GuildId == guildId);
                return template?.Text;
            }
        }

        public void SetBanTemplate(ulong guildId, string text)
        {
            using (var uow = _db.GetDbContext())
            {
                var template = uow._context.BanTemplates
                    .AsQueryable()
                    .FirstOrDefault(x => x.GuildId == guildId);

                if (text == null)
                {
                    if (template is null)
                        return;

                    uow._context.Remove(template);
                }
                else if (template == null)
                {
                    uow._context.BanTemplates.Add(new BanTemplate
                    {
                        GuildId = guildId,
                        Text = text
                    });
                }
                else
                {
                    template.Text = text;
                }

                uow.SaveChanges();
            }
        }

        public CREmbed GetBanUserDmEmbed(ICommandContext context, IGuildUser target, string defaultMessage,
            string banReason, TimeSpan? duration)
        {
            return GetBanUserDmEmbed(
                (DiscordSocketClient)context.Client,
                (SocketGuild)context.Guild,
                (IGuildUser)context.User,
                target,
                defaultMessage,
                banReason,
                duration);
        }

        public CREmbed GetBanUserDmEmbed(DiscordSocketClient client, SocketGuild guild,
            IGuildUser moderator, IGuildUser target, string defaultMessage, string banReason, TimeSpan? duration)
        {
            var template = GetBanTemplate(guild.Id);

            banReason = string.IsNullOrWhiteSpace(banReason)
                ? "-"
                : banReason;

            var replacer = new ReplacementBuilder()
                .WithServer(client, guild)
                .WithOverride("%ban.mod%", () => moderator.ToString())
                .WithOverride("%ban.mod.fullname%", () => moderator.ToString())
                .WithOverride("%ban.mod.name%", () => moderator.Username)
                .WithOverride("%ban.mod.discrim%", () => moderator.Discriminator)
                .WithOverride("%ban.user%", () => target.ToString())
                .WithOverride("%ban.user.fullname%", () => target.ToString())
                .WithOverride("%ban.user.name%", () => target.Username)
                .WithOverride("%ban.user.discrim%", () => target.Discriminator)
                .WithOverride("%reason%", () => banReason)
                .WithOverride("%ban.reason%", () => banReason)
                .WithOverride("%ban.duration%", () => duration?.ToString(@"d\.hh\:mm") ?? "perma")
                .Build();

            CREmbed crEmbed = null;
            // if template isn't set, use the old message style
            if (string.IsNullOrWhiteSpace(template))
            {
                template = JsonConvert.SerializeObject(new
                {
                    color = Mewdeko.ErrorColor.RawValue,
                    description = defaultMessage
                });

                CREmbed.TryParse(template, out crEmbed);
            }
            // if template is set to "-" do not dm the user
            else if (template == "-")
            {
                return default;
            }
            // if template is an embed, send that embed with replacements
            else if (CREmbed.TryParse(template, out crEmbed))
            {
                replacer.Replace(crEmbed);
            }
            // otherwise, treat template as a regular string with replacements
            else
            {
                template = JsonConvert.SerializeObject(new
                {
                    color = Mewdeko.ErrorColor.RawValue,
                    description = replacer.Replace(template)
                });

                CREmbed.TryParse(template, out crEmbed);
            }

            return crEmbed;
        }
    }
}