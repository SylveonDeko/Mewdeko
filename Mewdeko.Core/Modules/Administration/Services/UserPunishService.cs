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
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NLog;

namespace Mewdeko.Modules.Administration.Services
{
    public enum WarnAction
    {
        Mute,
        Addrole,
        Ban
    }

    public class UserPunishService : INService
    {
        private readonly Mewdeko _bot;
        private readonly DbService _db;
        private readonly Logger _log;
        private readonly MuteService _mute;
        private readonly Timer _warnExpiryTimer;

        public UserPunishService(MuteService mute, DbService db, Mewdeko bot)
        {
            _warnlogchannelids = bot.AllGuildConfigs
                .Where(x => x.WarnlogChannelId != 0)
                .ToDictionary(x => x.GuildId, x => x.WarnlogChannelId)
                .ToConcurrent();
            _mute = mute;
            _bot = bot;
            _db = db;
            _log = LogManager.GetCurrentClassLogger();

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
                (DiscordSocketClient) context.Client,
                (SocketGuild) context.Guild,
                (IGuildUser) context.User,
                target,
                defaultMessage,
                banReason,
                duration);
        }


        public CREmbed GetBanUserDmEmbed(DiscordSocketClient client, SocketGuild guild,
            IGuildUser moderator, IGuildUser target, string defaultMessage, string banReason, TimeSpan? duration)
        {
            var embed = new EmbedBuilder();
            var template = GetBanTemplate(guild.Id);
            var plainText = string.Empty;

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

                await uow.SaveChangesAsync();
            }

            var p = ps.FirstOrDefault(x => x.Count == warnings);

            if (p != null)
            {
                var user = await guild.GetUserAsync(userId).ConfigureAwait(false);
                if (user == null)
                    return null;
                var muteReason = "Warning punishment - " + reason;
                switch (p.Punishment)
                {
                    case PunishmentAction.Mute:
                        if (p.Time == 0)
                            await _mute.MuteUser(user, mod, reason: muteReason).ConfigureAwait(false);
                        else
                            await _mute.TimedMute(user, mod, TimeSpan.FromMinutes(p.Time), reason: muteReason)
                                .ConfigureAwait(false);
                        break;
                    case PunishmentAction.VoiceMute:
                        if (p.Time == 0)
                            await _mute.MuteUser(user, mod, MuteType.Voice, muteReason).ConfigureAwait(false);
                        else
                            await _mute.TimedMute(user, mod, TimeSpan.FromMinutes(p.Time), MuteType.Voice, muteReason)
                                .ConfigureAwait(false);
                        break;
                    case PunishmentAction.ChatMute:
                        if (p.Time == 0)
                            await _mute.MuteUser(user, mod, MuteType.Chat, muteReason).ConfigureAwait(false);
                        else
                            await _mute.TimedMute(user, mod, TimeSpan.FromMinutes(p.Time), MuteType.Chat, muteReason)
                                .ConfigureAwait(false);
                        break;
                    case PunishmentAction.Kick:
                        await user.KickAsync("Warned too many times.").ConfigureAwait(false);
                        break;
                    case PunishmentAction.Ban:
                        if (p.Time == 0)

                            await guild.AddBanAsync(user, reason: "Warned too many times.").ConfigureAwait(false);
                        //await guild2.AddBanAsync(user, reason: "Warned too many times.");
                        //await guild3.AddBanAsync(user, reason: "Warned too many times.");

                        else
                            await _mute.TimedBan(user, TimeSpan.FromMinutes(p.Time), "Warned too many times.")
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
                            _log.Warn($"Warnpunish can't find role {p.RoleId.Value} on server {guild.Id}");
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
                warnings = uow.Warnings
                    .ForId(guild.Id, userId)
                    .Count(w => !w.Forgiven && w.UserId == userId);
            }

            return warnings;
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
                    _log.Info($"Cleared {cleared} warnings and deleted {deleted} warnings due to expiry.");
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
                var ps = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.WarnPunishments)).WarnPunishments;
                var toDelete = ps.Where(x => x.Count == number);

                uow._context.RemoveRange(toDelete);

                ps.Add(new WarningPunishment
                {
                    Count = number,
                    Punishment = punish,
                    Time = (int?) time?.Time.TotalMinutes ?? 0,
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
                .ToArray();

            using (var uow = _db.GetDbContext())
            {
                var bc = uow.BotConfig.GetOrCreate(set => set.Include(x => x.Blacklist));
                //blacklist the users
                bc.Blacklist.AddRange(found.Select(x =>
                    new BlacklistItem
                    {
                        ItemId = x,
                        Type = BlacklistType.User
                    }));
                //clear their currencies
                uow.DiscordUsers.RemoveFromMany(found.Select(x => x).ToList());
                uow.SaveChanges();
            }

            return (bans, missing);
        }
    }
}