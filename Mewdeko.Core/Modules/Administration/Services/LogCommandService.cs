#if !GLOBAL_Mewdeko
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Mewdeko.Common.Collections;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Modules.Administration.Services
{
    public class LogCommandService : INService
    {
        private readonly DiscordSocketClient _client;
        
        public ConcurrentDictionary<ulong, LogSetting> GuildLogSettings { get; }

        private ConcurrentDictionary<ITextChannel, List<string>> PresenceUpdates { get; } =
            new ConcurrentDictionary<ITextChannel, List<string>>();

        private readonly Timer _timerReference;
        private readonly IBotStrings _strings;
        private readonly DbService _db;
        private readonly MuteService _mute;
        private readonly ProtectionService _prot;
        private readonly GuildTimezoneService _tz;

        public LogCommandService(DiscordSocketClient client, IBotStrings strings,
            DbService db, MuteService mute, ProtectionService prot, GuildTimezoneService tz, 
            IMemoryCache memoryCache)
        {
            _client = client;
            _memoryCache = memoryCache; 
            _strings = strings;
            _db = db;
            _mute = mute;
            _prot = prot;
            _tz = tz;

            using (var uow = db.GetDbContext())
            {
                var guildIds = client.Guilds.Select(x => x.Id).ToList();
                var configs = uow._context
                    .Set<GuildConfig>()
                    .AsQueryable()
                    .Include(gc => gc.LogSetting)
                    .ThenInclude(ls => ls.IgnoredChannels)
                    .Where(x => guildIds.Contains(x.GuildId))
                    .ToList();

                GuildLogSettings = configs
                    .ToDictionary(g => g.GuildId, g => g.LogSetting)
                    .ToConcurrent();
            }

            _timerReference = new Timer(async (state) =>
            {
                var keys = PresenceUpdates.Keys.ToList();

                await Task.WhenAll(keys.Select(key =>
                {
                    if (!((SocketGuild) key.Guild).CurrentUser.GetPermissions(key).SendMessages)
                        return Task.CompletedTask;
                    if (PresenceUpdates.TryRemove(key, out var msgs))
                    {
                        var title = GetText(key.Guild, "presence_updates");
                        var desc = string.Join(Environment.NewLine, msgs);
                        return key.SendConfirmAsync(title, desc.TrimTo(2048));
                    }

                    return Task.CompletedTask;
                })).ConfigureAwait(false);
            }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

            //_client.MessageReceived += _client_MessageReceived;
            _client.MessageUpdated += _client_MessageUpdated;
            _client.MessageDeleted += _client_MessageDeleted;
            _client.UserBanned += _client_UserBanned;
            _client.UserUnbanned += _client_UserUnbanned;
            _client.UserJoined += _client_UserJoined;
            _client.UserLeft += _client_UserLeft;
            //_client.UserPresenceUpdated += _client_UserPresenceUpdated;
            _client.UserVoiceStateUpdated += _client_UserVoiceStateUpdated;
            _client.UserVoiceStateUpdated += _client_UserVoiceStateUpdated_TTS;
            _client.GuildMemberUpdated += _client_GuildUserUpdated;
#if !GLOBAL_Mewdeko
            _client.UserUpdated += _client_UserUpdated;
#endif
            _client.ChannelCreated += _client_ChannelCreated;
            _client.ChannelDestroyed += _client_ChannelDestroyed;
            _client.ChannelUpdated += _client_ChannelUpdated;
            _client.RoleDeleted += _client_RoleDeleted;

            _mute.UserMuted += MuteCommands_UserMuted;
            _mute.UserUnmuted += MuteCommands_UserUnmuted;

            _prot.OnAntiProtectionTriggered += TriggeredAntiProtection;

            _clearTimer = new Timer(_ =>
            {
                _ignoreMessageIds.Clear();
            }, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        private readonly Timer _clearTimer;
        private readonly ConcurrentHashSet<ulong> _ignoreMessageIds = new ConcurrentHashSet<ulong>();
        private readonly IMemoryCache _memoryCache;

        public void AddDeleteIgnore(ulong messageId)
        {
            _ignoreMessageIds.Add(messageId);
        }

        public bool LogIgnore(ulong gid, ulong cid)
        {
            int removed = 0;
            using (var uow = _db.GetDbContext())
            {
                var config = uow.GuildConfigs.LogSettingsFor(gid);
                LogSetting logSetting = GuildLogSettings.GetOrAdd(gid, (id) => config.LogSetting);
                removed = logSetting.IgnoredChannels.RemoveWhere(ilc => ilc.ChannelId == cid);
                config.LogSetting.IgnoredChannels.RemoveWhere(ilc => ilc.ChannelId == cid);
                if (removed == 0)
                {
                    var toAdd = new IgnoredLogChannel {ChannelId = cid};
                    logSetting.IgnoredChannels.Add(toAdd);
                    config.LogSetting.IgnoredChannels.Add(toAdd);
                }

                uow.SaveChanges();
            }

            return removed > 0;
        }

        private string GetText(IGuild guild, string key, params object[] replacements) =>
            _strings.GetText(key, guild.Id, replacements);


        private string PrettyCurrentTime(IGuild g)
        {
            var time = DateTime.UtcNow;
            if (g != null)
                time = TimeZoneInfo.ConvertTime(time, _tz.GetTimeZoneOrUtc(g.Id));
            return $"【{time:HH:mm:ss}】";
        }

        private string CurrentTime(IGuild g)
        {
            DateTime time = DateTime.UtcNow;
            if (g != null)
                time = TimeZoneInfo.ConvertTime(time, _tz.GetTimeZoneOrUtc(g.Id));

            return $"{time:HH:mm:ss}";
        }

        public async Task LogServer(ulong guildId, ulong channelId, bool value)
        {
            LogSetting logSetting;
            using (var uow = _db.GetDbContext())
            {
                logSetting = uow.GuildConfigs.LogSettingsFor(guildId).LogSetting;
                GuildLogSettings.AddOrUpdate(guildId, (id) => logSetting, (id, old) => logSetting);
                logSetting.LogOtherId =
                logSetting.MessageUpdatedId =
                logSetting.MessageDeletedId =
                logSetting.UserJoinedId =
                logSetting.UserLeftId =
                logSetting.UserBannedId =
                logSetting.UserUnbannedId =
                logSetting.UserUpdatedId =
                logSetting.ChannelCreatedId =
                logSetting.ChannelDestroyedId =
                logSetting.ChannelUpdatedId =
                logSetting.LogUserPresenceId =
                logSetting.LogVoicePresenceId =
                logSetting.UserMutedId =
                logSetting.LogVoicePresenceTTSId =
                    (value ? channelId : (ulong?) null);

                await uow.SaveChangesAsync();
            }
        }

        private Task _client_UserUpdated(SocketUser before, SocketUser uAfter)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!(uAfter is SocketGuildUser after))
                        return;

                    var g = after.Guild;

                    if (!GuildLogSettings.TryGetValue(g.Id, out LogSetting logSetting)
                        || (logSetting.UserUpdatedId == null))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel =
                        await TryGetLogChannel(g, logSetting, LogType.UserUpdated).ConfigureAwait(false)) == null)
                        return;

                    var embed = new EmbedBuilder();

                    if (before.Username != after.Username)
                    {
                        embed.WithTitle("👥 " + GetText(g, "username_changed"))
                            .WithDescription($"{before.Username}#{before.Discriminator} | {before.Id}")
                            .AddField(fb => fb.WithName("Old Name").WithValue($"{before.Username}").WithIsInline(true))
                            .AddField(fb => fb.WithName("New Name").WithValue($"{after.Username}").WithIsInline(true))
                            .WithFooter(fb => fb.WithText(CurrentTime(g)))
                            .WithOkColor();
                    }
                    else if (before.AvatarId != after.AvatarId)
                    {
                        embed.WithTitle("👥" + GetText(g, "avatar_changed"))
                            .WithDescription($"{before.Username}#{before.Discriminator} | {before.Id}")
                            .WithFooter(fb => fb.WithText(CurrentTime(g)))
                            .WithOkColor();

                        var bav = before.RealAvatarUrl(2048);
                        if (bav != null && bav.IsAbsoluteUri)
                            embed.WithThumbnailUrl(bav.ToString());

                        var aav = after.RealAvatarUrl(2048);
                        if (aav != null && aav.IsAbsoluteUri)
                            embed.WithImageUrl(aav.ToString());
                    }
                    else
                    {
                        return;
                    }

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        public bool Log(ulong gid, ulong? cid, LogType type/*, string options*/)
        {
            ulong? channelId = null;
            using (var uow = _db.GetDbContext())
            {
                var logSetting = uow.GuildConfigs.LogSettingsFor(gid).LogSetting;
                GuildLogSettings.AddOrUpdate(gid, (id) => logSetting, (id, old) => logSetting);
                switch (type)
                {
                    case LogType.Other:
                        channelId = logSetting.LogOtherId = (logSetting.LogOtherId == null ? cid : default);
                        break;
                    case LogType.MessageUpdated:
                        channelId = logSetting.MessageUpdatedId = (logSetting.MessageUpdatedId == null ? cid : default);
                        break;
                    case LogType.MessageDeleted:
                        channelId = logSetting.MessageDeletedId = (logSetting.MessageDeletedId == null ? cid : default);
                        //logSetting.DontLogBotMessageDeleted = (options == "nobot");
                        break;
                    case LogType.UserJoined:
                        channelId = logSetting.UserJoinedId = (logSetting.UserJoinedId == null ? cid : default);
                        break;
                    case LogType.UserLeft:
                        channelId = logSetting.UserLeftId = (logSetting.UserLeftId == null ? cid : default);
                        break;
                    case LogType.UserBanned:
                        channelId = logSetting.UserBannedId = (logSetting.UserBannedId == null ? cid : default);
                        break;
                    case LogType.UserUnbanned:
                        channelId = logSetting.UserUnbannedId = (logSetting.UserUnbannedId == null ? cid : default);
                        break;
                    case LogType.UserUpdated:
                        channelId = logSetting.UserUpdatedId = (logSetting.UserUpdatedId == null ? cid : default);
                        break;
                    case LogType.UserMuted:
                        channelId = logSetting.UserMutedId = (logSetting.UserMutedId == null ? cid : default);
                        break;
                    case LogType.ChannelCreated:
                        channelId = logSetting.ChannelCreatedId = (logSetting.ChannelCreatedId == null ? cid : default);
                        break;
                    case LogType.ChannelDestroyed:
                        channelId = logSetting.ChannelDestroyedId =
                            (logSetting.ChannelDestroyedId == null ? cid : default);
                        break;
                    case LogType.ChannelUpdated:
                        channelId = logSetting.ChannelUpdatedId = (logSetting.ChannelUpdatedId == null ? cid : default);
                        break;
                    case LogType.UserPresence:
                        channelId = logSetting.LogUserPresenceId =
                            (logSetting.LogUserPresenceId == null ? cid : default);
                        break;
                    case LogType.VoicePresence:
                        channelId = logSetting.LogVoicePresenceId =
                            (logSetting.LogVoicePresenceId == null ? cid : default);
                        break;
                    case LogType.VoicePresenceTTS:
                        channelId = logSetting.LogVoicePresenceTTSId =
                            (logSetting.LogVoicePresenceTTSId == null ? cid : default);
                        break;
                }

                uow.SaveChanges();
            }

            return channelId != null;
        }

        private Task _client_UserVoiceStateUpdated_TTS(SocketUser iusr, SocketVoiceState before, SocketVoiceState after)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!(iusr is IGuildUser usr))
                        return;

                    var beforeVch = before.VoiceChannel;
                    var afterVch = after.VoiceChannel;

                    if (beforeVch == afterVch)
                        return;

                    if (!GuildLogSettings.TryGetValue(usr.Guild.Id, out LogSetting logSetting)
                        || (logSetting.LogVoicePresenceTTSId == null))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(usr.Guild, logSetting, LogType.VoicePresenceTTS)
                        .ConfigureAwait(false)) == null)
                        return;

                    var str = "";
                    if (beforeVch?.Guild == afterVch?.Guild)
                    {
                        str = GetText(logChannel.Guild, "log_vc_moved", usr.Username, beforeVch?.Name, afterVch?.Name);
                    }
                    else if (beforeVch == null)
                    {
                        str = GetText(logChannel.Guild, "log_vc_joined", usr.Username, afterVch.Name);
                    }
                    else if (afterVch == null)
                    {
                        str = GetText(logChannel.Guild, "log_vc_left", usr.Username, beforeVch.Name);
                    }

                    var toDelete = await logChannel.SendMessageAsync(str, true).ConfigureAwait(false);
                    toDelete.DeleteAfter(5);
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private void MuteCommands_UserMuted(IGuildUser usr, IUser mod, MuteType muteType, string reason)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!GuildLogSettings.TryGetValue(usr.Guild.Id, out LogSetting logSetting)
                        || (logSetting.UserMutedId == null))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(usr.Guild, logSetting, LogType.UserMuted)
                        .ConfigureAwait(false)) == null)
                        return;
                    var mutes = "";
                    var mutedLocalized = GetText(logChannel.Guild, "muted_sn");
                    switch (muteType)
                    {
                        case MuteType.Voice:
                            mutes = "🔇 " + GetText(logChannel.Guild, "xmuted_voice", mutedLocalized, mod.ToString());
                            break;
                        case MuteType.Chat:
                            mutes = "🔇 " + GetText(logChannel.Guild, "xmuted_text", mutedLocalized, mod.ToString());
                            break;
                        case MuteType.All:
                            mutes = "🔇 " + GetText(logChannel.Guild, "xmuted_text_and_voice", mutedLocalized,
                                mod.ToString());
                            break;
                    }

                    var embed = new EmbedBuilder().WithAuthor(eab => eab.WithName(mutes))
                        .WithTitle($"{usr.Username}#{usr.Discriminator} | {usr.Id}")
                        .WithFooter(fb => fb.WithText(CurrentTime(usr.Guild)))
                        .WithOkColor();

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            });
        }

        private void MuteCommands_UserUnmuted(IGuildUser usr, IUser mod, MuteType muteType, string reason)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!GuildLogSettings.TryGetValue(usr.Guild.Id, out LogSetting logSetting)
                        || (logSetting.UserMutedId == null))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(usr.Guild, logSetting, LogType.UserMuted)
                        .ConfigureAwait(false)) == null)
                        return;

                    var mutes = "";
                    var unmutedLocalized = GetText(logChannel.Guild, "unmuted_sn");
                    switch (muteType)
                    {
                        case MuteType.Voice:
                            mutes = "🔊 " + GetText(logChannel.Guild, "xmuted_voice", unmutedLocalized, mod.ToString());
                            break;
                        case MuteType.Chat:
                            mutes = "🔊 " + GetText(logChannel.Guild, "xmuted_text", unmutedLocalized, mod.ToString());
                            break;
                        case MuteType.All:
                            mutes = "🔊 " + GetText(logChannel.Guild, "xmuted_text_and_voice", unmutedLocalized,
                                mod.ToString());
                            break;
                    }

                    var embed = new EmbedBuilder().WithAuthor(eab => eab.WithName(mutes))
                        .WithTitle($"{usr.Username}#{usr.Discriminator} | {usr.Id}")
                        .WithFooter(fb => fb.WithText($"{CurrentTime(usr.Guild)}"))
                        .WithOkColor();

                    if (!string.IsNullOrWhiteSpace(reason))
                        embed.WithDescription(reason);

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            });
        }

        public Task TriggeredAntiProtection(PunishmentAction action, ProtectionType protection,
            params IGuildUser[] users)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (users.Length == 0)
                        return;

                    if (!GuildLogSettings.TryGetValue(users.First().Guild.Id, out LogSetting logSetting)
                        || (logSetting.LogOtherId == null))
                        return;
                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(users.First().Guild, logSetting, LogType.Other)
                        .ConfigureAwait(false)) == null)
                        return;

                    var punishment = "";
                    switch (action)
                    {
                        case PunishmentAction.Mute:
                            punishment = "🔇 " + GetText(logChannel.Guild, "muted_pl").ToUpperInvariant();
                            break;
                        case PunishmentAction.Kick:
                            punishment = "👢 " + GetText(logChannel.Guild, "kicked_pl").ToUpperInvariant();
                            break;
                        case PunishmentAction.Softban:
                            punishment = "☣ " + GetText(logChannel.Guild, "soft_banned_pl").ToUpperInvariant();
                            break;
                        case PunishmentAction.Ban:
                            punishment = "⛔️ " + GetText(logChannel.Guild, "banned_pl").ToUpperInvariant();
                            break;
                        case PunishmentAction.RemoveRoles:
                            punishment = "⛔️ " + GetText(logChannel.Guild, "remove_roles_pl").ToUpperInvariant();
                            break;
                    }

                    var embed = new EmbedBuilder().WithAuthor(eab => eab.WithName($"🛡 Anti-{protection}"))
                        .WithTitle(GetText(logChannel.Guild, "users") + " " + punishment)
                        .WithDescription(string.Join("\n", users.Select(u => u.ToString())))
                        .WithFooter(fb => fb.WithText(CurrentTime(logChannel.Guild)))
                        .WithOkColor();

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private string GetRoleDeletedKey(ulong roleId)
            => $"role_deleted_{roleId}";
        
        private Task _client_RoleDeleted(SocketRole socketRole)
        {
            Serilog.Log.Information("Role deleted {RoleId}", socketRole.Id);
            _memoryCache.Set(GetRoleDeletedKey(socketRole.Id), 
                true,
                TimeSpan.FromMinutes(5));
            return Task.CompletedTask;
        }

        private bool IsRoleDeleted(ulong roleId)
        {
            var isDeleted = _memoryCache.TryGetValue(GetRoleDeletedKey(roleId), out var _);
            return isDeleted;
        }

        private Task _client_GuildUserUpdated(SocketGuildUser before, SocketGuildUser after)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!GuildLogSettings.TryGetValue(before.Guild.Id, out LogSetting logSetting))
                        return;

                    ITextChannel logChannel;
                    if (logSetting.UserUpdatedId != null &&
                        (logChannel = await TryGetLogChannel(before.Guild, logSetting, LogType.UserUpdated)
                            .ConfigureAwait(false)) != null)
                    {
                        var embed = new EmbedBuilder().WithOkColor()
                            .WithFooter(efb => efb.WithText(CurrentTime(before.Guild)))
                            .WithTitle($"{before.Username}#{before.Discriminator} | {before.Id}");
                        if (before.Nickname != after.Nickname)
                        {
                            embed.WithAuthor(eab => eab.WithName("👥 " + GetText(logChannel.Guild, "nick_change")))
                                .AddField(efb =>
                                    efb.WithName(GetText(logChannel.Guild, "old_nick"))
                                        .WithValue($"{before.Nickname}#{before.Discriminator}"))
                                .AddField(efb =>
                                    efb.WithName(GetText(logChannel.Guild, "new_nick"))
                                        .WithValue($"{after.Nickname}#{after.Discriminator}"));

                            await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                        }
                        else if (!before.Roles.SequenceEqual(after.Roles))
                        {
                            if (before.Roles.Count < after.Roles.Count)
                            {
                                var diffRoles = after.Roles.Where(r => !before.Roles.Contains(r)).Select(r => r.Name);
                                embed.WithAuthor(eab => eab.WithName("⚔ " + GetText(logChannel.Guild, "user_role_add")))
                                    .WithDescription(string.Join(", ", diffRoles).SanitizeMentions());

                                await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                            }
                            else if (before.Roles.Count > after.Roles.Count)
                            {
                                await Task.Delay(1000);
                                var diffRoles = before.Roles
                                    .Where(r => !after.Roles.Contains(r) && !IsRoleDeleted(r.Id))
                                    .Select(r => r.Name)
                                    .ToList();

                                if (diffRoles.Any())
                                {
                                    embed.WithAuthor(eab =>
                                            eab.WithName("⚔ " + GetText(logChannel.Guild, "user_role_rem")))
                                        .WithDescription(string.Join(", ", diffRoles).SanitizeMentions());

                                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                                }
                            }
                        }
                    }

                    logChannel = null;
                    if (!before.IsBot && logSetting.LogUserPresenceId != null && (logChannel =
                        await TryGetLogChannel(before.Guild, logSetting, LogType.UserPresence)
                            .ConfigureAwait(false)) != null)
                    {
                        if (before.Status != after.Status)
                        {
                            var str = "🎭" + Format.Code(PrettyCurrentTime(after.Guild)) +
                                      GetText(logChannel.Guild, "user_status_change",
                                          "👤" + Format.Bold(after.Username),
                                          Format.Bold(after.Status.ToString()));
                            PresenceUpdates.AddOrUpdate(logChannel,
                                new List<string>() {str}, (id, list) =>
                                {
                                    list.Add(str);
                                    return list;
                                });
                        }
                        else if (before.Activity?.Name != after.Activity?.Name)
                        {
                            var str =
                                $"👾`{PrettyCurrentTime(after.Guild)}`👤__**{after.Username}**__ is now playing **{after.Activity?.Name ?? "-"}**.";
                            PresenceUpdates.AddOrUpdate(logChannel,
                                new List<string>() {str}, (id, list) =>
                                {
                                    list.Add(str);
                                    return list;
                                });
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private Task _client_ChannelUpdated(IChannel cbefore, IChannel cafter)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!(cbefore is IGuildChannel before))
                        return;

                    var after = (IGuildChannel) cafter;

                    if (!GuildLogSettings.TryGetValue(before.Guild.Id, out LogSetting logSetting)
                        || (logSetting.ChannelUpdatedId == null)
                        || logSetting.IgnoredChannels.Any(ilc => ilc.ChannelId == after.Id))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(before.Guild, logSetting, LogType.ChannelUpdated)
                        .ConfigureAwait(false)) == null)
                        return;

                    var embed = new EmbedBuilder().WithOkColor()
                        .WithFooter(efb => efb.WithText(CurrentTime(before.Guild)));

                    var beforeTextChannel = cbefore as ITextChannel;
                    var afterTextChannel = cafter as ITextChannel;

                    if (before.Name != after.Name)
                    {
                        embed.WithTitle("ℹ️ " + GetText(logChannel.Guild, "ch_name_change"))
                            .WithDescription($"{after} | {after.Id}")
                            .AddField(efb =>
                                efb.WithName(GetText(logChannel.Guild, "ch_old_name")).WithValue(before.Name));
                    }
                    else if (beforeTextChannel?.Topic != afterTextChannel?.Topic)
                    {
                        embed.WithTitle("ℹ️ " + GetText(logChannel.Guild, "ch_topic_change"))
                            .WithDescription($"{after} | {after.Id}")
                            .AddField(efb =>
                                efb.WithName(GetText(logChannel.Guild, "old_topic"))
                                    .WithValue(beforeTextChannel?.Topic ?? "-"))
                            .AddField(efb =>
                                efb.WithName(GetText(logChannel.Guild, "new_topic"))
                                    .WithValue(afterTextChannel?.Topic ?? "-"));
                    }
                    else
                        return;

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private Task _client_ChannelDestroyed(IChannel ich)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!(ich is IGuildChannel ch))
                        return;

                    if (!GuildLogSettings.TryGetValue(ch.Guild.Id, out LogSetting logSetting)
                        || (logSetting.ChannelDestroyedId == null)
                        || logSetting.IgnoredChannels.Any(ilc => ilc.ChannelId == ch.Id))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(ch.Guild, logSetting, LogType.ChannelDestroyed)
                        .ConfigureAwait(false)) == null)
                        return;
                    string title;
                    if (ch is IVoiceChannel)
                    {
                        title = GetText(logChannel.Guild, "voice_chan_destroyed");
                    }
                    else
                        title = GetText(logChannel.Guild, "text_chan_destroyed");

                    await logChannel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("🆕 " + title)
                        .WithDescription($"{ch.Name} | {ch.Id}")
                        .WithFooter(efb => efb.WithText(CurrentTime(ch.Guild)))).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private Task _client_ChannelCreated(IChannel ich)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!(ich is IGuildChannel ch))
                        return;

                    if (!GuildLogSettings.TryGetValue(ch.Guild.Id, out LogSetting logSetting)
                        || (logSetting.ChannelCreatedId == null))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(ch.Guild, logSetting, LogType.ChannelCreated)
                        .ConfigureAwait(false)) == null)
                        return;
                    string title;
                    if (ch is IVoiceChannel)
                    {
                        title = GetText(logChannel.Guild, "voice_chan_created");
                    }
                    else
                        title = GetText(logChannel.Guild, "text_chan_created");

                    await logChannel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("🆕 " + title)
                        .WithDescription($"{ch.Name} | {ch.Id}")
                        .WithFooter(efb => efb.WithText(CurrentTime(ch.Guild)))).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private Task _client_UserVoiceStateUpdated(SocketUser iusr, SocketVoiceState before, SocketVoiceState after)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!(iusr is IGuildUser usr) || usr.IsBot)
                        return;

                    var beforeVch = before.VoiceChannel;
                    var afterVch = after.VoiceChannel;

                    if (beforeVch == afterVch)
                        return;

                    if (!GuildLogSettings.TryGetValue(usr.Guild.Id, out LogSetting logSetting)
                        || (logSetting.LogVoicePresenceId == null))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(usr.Guild, logSetting, LogType.VoicePresence)
                        .ConfigureAwait(false)) == null)
                        return;

                    string str = null;
                    if (beforeVch?.Guild == afterVch?.Guild)
                    {
                        str = "🎙" + Format.Code(PrettyCurrentTime(usr.Guild)) + GetText(logChannel.Guild,
                            "user_vmoved",
                            "👤" + Format.Bold(usr.Username + "#" + usr.Discriminator),
                            Format.Bold(beforeVch?.Name ?? ""), Format.Bold(afterVch?.Name ?? ""));
                    }
                    else if (beforeVch == null)
                    {
                        str = "🎙" + Format.Code(PrettyCurrentTime(usr.Guild)) + GetText(logChannel.Guild,
                            "user_vjoined",
                            "👤" + Format.Bold(usr.Username + "#" + usr.Discriminator),
                            Format.Bold(afterVch.Name ?? ""));
                    }
                    else if (afterVch == null)
                    {
                        str = "🎙" + Format.Code(PrettyCurrentTime(usr.Guild)) + GetText(logChannel.Guild, "user_vleft",
                            "👤" + Format.Bold(usr.Username + "#" + usr.Discriminator),
                            Format.Bold(beforeVch.Name ?? ""));
                    }

                    if (!string.IsNullOrWhiteSpace(str))
                        PresenceUpdates.AddOrUpdate(logChannel, new List<string>() {str}, (id, list) =>
                        {
                            list.Add(str);
                            return list;
                        });
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        //private Task _client_UserPresenceUpdated(Optional<SocketGuild> optGuild, SocketUser usr, SocketPresence before, SocketPresence after)
        //{
        //    var _ = Task.Run(async () =>
        //    {
        //        try
        //        {
        //            var guild = optGuild.GetValueOrDefault() ?? (usr as SocketGuildUser)?.Guild;

        //            if (guild == null)
        //                return;

        //            if (!GuildLogSettings.TryGetValue(guild.Id, out LogSetting logSetting)
        //                || (logSetting.LogUserPresenceId == null)
        //                || before.Status == after.Status)
        //                return;

        //            ITextChannel logChannel;
        //            if ((logChannel = await TryGetLogChannel(guild, logSetting, LogType.UserPresence)) == null)
        //                return;
        //            string str = "";
        //            if (before.Status != after.Status)
        //                str = "🎭" + Format.Code(PrettyCurrentTime(g)) +
        //                      GetText(logChannel.Guild, "user_status_change",
        //                            "👤" + Format.Bold(usr.Username),
        //                            Format.Bold(after.Status.ToString()));

        //            //if (before.Game?.Name != after.Game?.Name)
        //            //{
        //            //    if (str != "")
        //            //        str += "\n";
        //            //    str += $"👾`{prettyCurrentTime}`👤__**{usr.Username}**__ is now playing **{after.Game?.Name}**.";
        //            //}

        //            PresenceUpdates.AddOrUpdate(logChannel, new List<string>() { str }, (id, list) => { list.Add(str); return list; });
        //        }
        //        catch
        //        {
        //            // ignored
        //        }
        //    });
        //    return Task.CompletedTask;
        //}

        private Task _client_UserLeft(IGuildUser usr)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!GuildLogSettings.TryGetValue(usr.Guild.Id, out LogSetting logSetting)
                        || (logSetting.UserLeftId == null))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(usr.Guild, logSetting, LogType.UserLeft)
                        .ConfigureAwait(false)) == null)
                        return;
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("❌ " + GetText(logChannel.Guild, "user_left"))
                        .WithDescription(usr.ToString())
                        .AddField(efb => efb.WithName("Id").WithValue(usr.Id.ToString()))
                        .WithFooter(efb => efb.WithText(CurrentTime(usr.Guild)));

                    if (Uri.IsWellFormedUriString(usr.GetAvatarUrl(), UriKind.Absolute))
                        embed.WithThumbnailUrl(usr.GetAvatarUrl());

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private Task _client_UserJoined(IGuildUser usr)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!GuildLogSettings.TryGetValue(usr.Guild.Id, out LogSetting logSetting)
                        || (logSetting.UserJoinedId == null))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(usr.Guild, logSetting, LogType.UserJoined)
                        .ConfigureAwait(false)) == null)
                        return;

                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("✅ " + GetText(logChannel.Guild, "user_joined"))
                        .WithDescription($"{usr.Mention} `{usr}`")
                        .AddField(efb => efb.WithName("Id").WithValue(usr.Id.ToString()))
                        .AddField(fb =>
                            fb.WithName(GetText(logChannel.Guild, "joined_server"))
                                .WithValue($"{usr.JoinedAt?.ToString("dd.MM.yyyy HH:mm") ?? "?"}").WithIsInline(true))
                        .AddField(fb =>
                            fb.WithName(GetText(logChannel.Guild, "joined_discord"))
                                .WithValue($"{usr.CreatedAt:dd.MM.yyyy HH:mm}").WithIsInline(true))
                        .WithFooter(efb => efb.WithText(CurrentTime(usr.Guild)));

                    if (Uri.IsWellFormedUriString(usr.GetAvatarUrl(), UriKind.Absolute))
                        embed.WithThumbnailUrl(usr.GetAvatarUrl());

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private Task _client_UserUnbanned(IUser usr, IGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!GuildLogSettings.TryGetValue(guild.Id, out LogSetting logSetting)
                        || (logSetting.UserUnbannedId == null))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(guild, logSetting, LogType.UserUnbanned)
                        .ConfigureAwait(false)) == null)
                        return;
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("♻️ " + GetText(logChannel.Guild, "user_unbanned"))
                        .WithDescription(usr.ToString())
                        .AddField(efb => efb.WithName("Id").WithValue(usr.Id.ToString()))
                        .WithFooter(efb => efb.WithText(CurrentTime(guild)));

                    if (Uri.IsWellFormedUriString(usr.GetAvatarUrl(), UriKind.Absolute))
                        embed.WithThumbnailUrl(usr.GetAvatarUrl());

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private Task _client_UserBanned(IUser usr, IGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!GuildLogSettings.TryGetValue(guild.Id, out LogSetting logSetting)
                        || (logSetting.UserBannedId == null))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel =
                            await TryGetLogChannel(guild, logSetting, LogType.UserBanned).ConfigureAwait(false)) ==
                        null)
                        return;
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("🚫 " + GetText(logChannel.Guild, "user_banned"))
                        .WithDescription(usr.ToString())
                        .AddField(efb => efb.WithName("Id").WithValue(usr.Id.ToString()))
                        .WithFooter(efb => efb.WithText(CurrentTime(guild)));

                    var avatarUrl = usr.GetAvatarUrl();

                    if (Uri.IsWellFormedUriString(avatarUrl, UriKind.Absolute))
                        embed.WithThumbnailUrl(usr.GetAvatarUrl());

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private Task _client_MessageDeleted(Cacheable<IMessage, ulong> optMsg, ISocketMessageChannel ch)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    var msg = (optMsg.HasValue ? optMsg.Value : null) as IUserMessage;
                    if (msg == null || msg.IsAuthor(_client))
                        return;

                    if (_ignoreMessageIds.Contains(msg.Id))
                        return;

                    if (!(ch is ITextChannel channel))
                        return;

                    if (!GuildLogSettings.TryGetValue(channel.Guild.Id, out LogSetting logSetting)
                        || (logSetting.MessageDeletedId == null)
                        || logSetting.IgnoredChannels.Any(ilc => ilc.ChannelId == channel.Id))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(channel.Guild, logSetting, LogType.MessageDeleted)
                        .ConfigureAwait(false)) == null || logChannel.Id == msg.Id)
                        return;

                    var resolvedMessage = msg.Resolve(userHandling: TagHandling.FullName);
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("🗑 " + GetText(logChannel.Guild, "msg_del", ((ITextChannel) msg.Channel).Name))
                        .WithDescription(msg.Author.ToString())
                        .AddField(efb =>
                            efb.WithName(GetText(logChannel.Guild, "content"))
                                .WithValue(string.IsNullOrWhiteSpace(resolvedMessage) ? "-" : resolvedMessage)
                                .WithIsInline(false))
                        .AddField(efb => efb.WithName("Id").WithValue(msg.Id.ToString()).WithIsInline(false))
                        .WithFooter(efb => efb.WithText(CurrentTime(channel.Guild)));
                    if (msg.Attachments.Any())
                        embed.AddField(efb =>
                            efb.WithName(GetText(logChannel.Guild, "attachments"))
                                .WithValue(string.Join(", ", msg.Attachments.Select(a => a.Url))).WithIsInline(false));

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        private Task _client_MessageUpdated(Cacheable<IMessage, ulong> optmsg, SocketMessage imsg2,
            ISocketMessageChannel ch)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    if (!(imsg2 is IUserMessage after) || after.IsAuthor(_client))
                        return;

                    var before = (optmsg.HasValue ? optmsg.Value : null) as IUserMessage;
                    if (before == null)
                        return;

                    if (!(ch is ITextChannel channel))
                        return;

                    if (before.Content == after.Content)
                        return;

                    if (before.Author.IsBot)
                        return;

                    if (!GuildLogSettings.TryGetValue(channel.Guild.Id, out LogSetting logSetting)
                        || (logSetting.MessageUpdatedId == null)
                        || logSetting.IgnoredChannels.Any(ilc => ilc.ChannelId == channel.Id))
                        return;

                    ITextChannel logChannel;
                    if ((logChannel = await TryGetLogChannel(channel.Guild, logSetting, LogType.MessageUpdated)
                        .ConfigureAwait(false)) == null || logChannel.Id == after.Channel.Id)
                        return;

                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("📝 " + GetText(logChannel.Guild, "msg_update", ((ITextChannel) after.Channel).Name))
                        .WithDescription(after.Author.ToString())
                        .AddField(efb =>
                            efb.WithName(GetText(logChannel.Guild, "old_msg"))
                                .WithValue(string.IsNullOrWhiteSpace(before.Content)
                                    ? "-"
                                    : before.Resolve(userHandling: TagHandling.FullName)).WithIsInline(false))
                        .AddField(efb =>
                            efb.WithName(GetText(logChannel.Guild, "new_msg"))
                                .WithValue(string.IsNullOrWhiteSpace(after.Content)
                                    ? "-"
                                    : after.Resolve(userHandling: TagHandling.FullName)).WithIsInline(false))
                        .AddField(efb => efb.WithName("Id").WithValue(after.Id.ToString()).WithIsInline(false))
                        .WithFooter(efb => efb.WithText(CurrentTime(channel.Guild)));

                    await logChannel.EmbedAsync(embed).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        public enum LogType
        {
            Other,
            MessageUpdated,
            MessageDeleted,
            UserJoined,
            UserLeft,
            UserBanned,
            UserUnbanned,
            UserUpdated,
            ChannelCreated,
            ChannelDestroyed,
            ChannelUpdated,
            UserPresence,
            VoicePresence,
            VoicePresenceTTS,
            UserMuted
        }

        private async Task<ITextChannel> TryGetLogChannel(IGuild guild, LogSetting logSetting, LogType logChannelType)
        {
            ulong? id = null;
            switch (logChannelType)
            {
                case LogType.Other:
                    id = logSetting.LogOtherId;
                    break;
                case LogType.MessageUpdated:
                    id = logSetting.MessageUpdatedId;
                    break;
                case LogType.MessageDeleted:
                    id = logSetting.MessageDeletedId;
                    break;
                case LogType.UserJoined:
                    id = logSetting.UserJoinedId;
                    break;
                case LogType.UserLeft:
                    id = logSetting.UserLeftId;
                    break;
                case LogType.UserBanned:
                    id = logSetting.UserBannedId;
                    break;
                case LogType.UserUnbanned:
                    id = logSetting.UserUnbannedId;
                    break;
                case LogType.UserUpdated:
                    id = logSetting.UserUpdatedId;
                    break;
                case LogType.ChannelCreated:
                    id = logSetting.ChannelCreatedId;
                    break;
                case LogType.ChannelDestroyed:
                    id = logSetting.ChannelDestroyedId;
                    break;
                case LogType.ChannelUpdated:
                    id = logSetting.ChannelUpdatedId;
                    break;
                case LogType.UserPresence:
                    id = logSetting.LogUserPresenceId;
                    break;
                case LogType.VoicePresence:
                    id = logSetting.LogVoicePresenceId;
                    break;
                case LogType.VoicePresenceTTS:
                    id = logSetting.LogVoicePresenceTTSId;
                    break;
                case LogType.UserMuted:
                    id = logSetting.UserMutedId;
                    break;
            }

            if (!id.HasValue || id == 0)
            {
                UnsetLogSetting(guild.Id, logChannelType);
                return null;
            }

            var channel = await guild.GetTextChannelAsync(id.Value).ConfigureAwait(false);

            if (channel == null)
            {
                UnsetLogSetting(guild.Id, logChannelType);
                return null;
            }
            else
                return channel;
        }

        private void UnsetLogSetting(ulong guildId, LogType logChannelType)
        {
            using (var uow = _db.GetDbContext())
            {
                var newLogSetting = uow.GuildConfigs.LogSettingsFor(guildId).LogSetting;
                switch (logChannelType)
                {
                    case LogType.Other:
                        newLogSetting.LogOtherId = null;
                        break;
                    case LogType.MessageUpdated:
                        newLogSetting.MessageUpdatedId = null;
                        break;
                    case LogType.MessageDeleted:
                        newLogSetting.MessageDeletedId = null;
                        break;
                    case LogType.UserJoined:
                        newLogSetting.UserJoinedId = null;
                        break;
                    case LogType.UserLeft:
                        newLogSetting.UserLeftId = null;
                        break;
                    case LogType.UserBanned:
                        newLogSetting.UserBannedId = null;
                        break;
                    case LogType.UserUnbanned:
                        newLogSetting.UserUnbannedId = null;
                        break;
                    case LogType.UserUpdated:
                        newLogSetting.UserUpdatedId = null;
                        break;
                    case LogType.UserMuted:
                        newLogSetting.UserMutedId = null;
                        break;
                    case LogType.ChannelCreated:
                        newLogSetting.ChannelCreatedId = null;
                        break;
                    case LogType.ChannelDestroyed:
                        newLogSetting.ChannelDestroyedId = null;
                        break;
                    case LogType.ChannelUpdated:
                        newLogSetting.ChannelUpdatedId = null;
                        break;
                    case LogType.UserPresence:
                        newLogSetting.LogUserPresenceId = null;
                        break;
                    case LogType.VoicePresence:
                        newLogSetting.LogVoicePresenceId = null;
                        break;
                    case LogType.VoicePresenceTTS:
                        newLogSetting.LogVoicePresenceTTSId = null;
                        break;
                }

                GuildLogSettings.AddOrUpdate(guildId, newLogSetting, (gid, old) => newLogSetting);
                uow.SaveChanges();
            }
        }
    }
}
#endif
