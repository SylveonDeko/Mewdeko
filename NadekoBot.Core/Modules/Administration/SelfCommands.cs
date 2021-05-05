using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using NadekoBot.Common;
using NadekoBot.Common.Attributes;
using NadekoBot.Common.Replacements;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Extensions;
using NadekoBot.Modules.Administration.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using NadekoBot.Core.Services;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class SelfCommands : NadekoSubmodule<SelfService>
        {
            private readonly DiscordSocketClient _client;
            private readonly NadekoBot _bot;
            private readonly IBotStrings _strings;

            public SelfCommands(DiscordSocketClient client, NadekoBot bot, IBotStrings strings)
            {
                _client = client;
                _bot = bot;
                _strings = strings;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [OwnerOnly]
            public async Task StartupCommandAdd([Leftover] string cmdText)
            {
                if (cmdText.StartsWith(Prefix + "die", StringComparison.InvariantCulture))
                    return;

                var guser = (IGuildUser)ctx.User;
                var cmd = new StartupCommand()
                {
                    CommandText = cmdText,
                    ChannelId = ctx.Channel.Id,
                    ChannelName = ctx.Channel.Name,
                    GuildId = ctx.Guild?.Id,
                    GuildName = ctx.Guild?.Name,
                    VoiceChannelId = guser.VoiceChannel?.Id,
                    VoiceChannelName = guser.VoiceChannel?.Name,
                    Interval = 0,
                };
                _service.AddNewAutoCommand(cmd);

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle(GetText("scadd"))
                    .AddField(efb => efb.WithName(GetText("server"))
                        .WithValue(cmd.GuildId == null ? $"-" : $"{cmd.GuildName}/{cmd.GuildId}").WithIsInline(true))
                    .AddField(efb => efb.WithName(GetText("channel"))
                        .WithValue($"{cmd.ChannelName}/{cmd.ChannelId}").WithIsInline(true))
                    .AddField(efb => efb.WithName(GetText("command_text"))
                        .WithValue(cmdText).WithIsInline(false))).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [OwnerOnly]
            public async Task AutoCommandAdd(int interval, [Leftover] string cmdText)
            {
                if (cmdText.StartsWith(Prefix + "die", StringComparison.InvariantCulture))
                    return;

                if (interval < 5)
                    return;

                var guser = (IGuildUser)ctx.User;
                var cmd = new StartupCommand()
                {
                    CommandText = cmdText,
                    ChannelId = ctx.Channel.Id,
                    ChannelName = ctx.Channel.Name,
                    GuildId = ctx.Guild?.Id,
                    GuildName = ctx.Guild?.Name,
                    VoiceChannelId = guser.VoiceChannel?.Id,
                    VoiceChannelName = guser.VoiceChannel?.Name,
                    Interval = interval,
                };
                _service.AddNewAutoCommand(cmd);

                await ReplyConfirmLocalizedAsync("autocmd_add", Format.Code(Format.Sanitize(cmdText)), cmd.Interval).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [OwnerOnly]
            public async Task StartupCommandsList(int page = 1)
            {
                if (page-- < 1)
                    return;

                var scmds = _service.GetStartupCommands()
                    .Where(x => x.Interval <= 0)
                    .Skip(page * 5)
                    .Take(5);
                if (!scmds.Any())
                {
                    await ReplyErrorLocalizedAsync("startcmdlist_none").ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync(
                        text: string.Join("\n", scmds
                        .Select(x => $@"```css
#{x.Index}
[{GetText("server")}]: {(x.GuildId.HasValue ? $"{x.GuildName} #{x.GuildId}" : "-")}
[{GetText("channel")}]: {x.ChannelName} #{x.ChannelId}
[{GetText("command_text")}]: {x.CommandText}```")),
                        title: string.Empty,
                        footer: GetText("page", page + 1))
                    .ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [OwnerOnly]
            public async Task AutoCommands(int page = 1)
            {
                if (page-- < 1)
                    return;

                var scmds = _service.GetStartupCommands()
                    .Where(x => x.Interval >= 5)
                    .Skip(page * 5)
                    .Take(5);
                if (!scmds.Any())
                {
                    await ReplyErrorLocalizedAsync("autocmdlist_none").ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel.SendConfirmAsync(
                        text: string.Join("\n", scmds
                        .Select(x => $@"```css
#{x.Index}
[{GetText("server")}]: {(x.GuildId.HasValue ? $"{x.GuildName} #{x.GuildId}" : "-")}
[{GetText("channel")}]: {x.ChannelName} #{x.ChannelId}
{GetIntervalText(x.Interval)}
[{GetText("command_text")}]: {x.CommandText}```")),
                        title: string.Empty,
                        footer: GetText("page", page + 1))
                    .ConfigureAwait(false);
                }
            }

            private string GetIntervalText(int interval)
            {
                return $"[{GetText("interval")}]: {interval}";
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task Wait(int miliseconds)
            {
                if (miliseconds <= 0)
                    return;
                ctx.Message.DeleteAfter(0);
                try
                {
                    var msg = await ctx.Channel.SendConfirmAsync($"⏲ {miliseconds}ms")
                        .ConfigureAwait(false);
                    msg.DeleteAfter(miliseconds / 1000);
                }
                catch { }

                await Task.Delay(miliseconds).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [OwnerOnly]
            public async Task StartupCommandRemove([Leftover] int index)
            {
                if (!_service.RemoveStartupCommand(index, out _))
                    await ReplyErrorLocalizedAsync("scrm_fail").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("scrm").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [OwnerOnly]
            public async Task StartupCommandsClear()
            {
                _service.ClearStartupCommands();

                await ReplyConfirmLocalizedAsync("startcmds_cleared").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ForwardMessages()
            {
                var enabled = _service.ForwardMessages();

                if (enabled)
                    await ReplyConfirmLocalizedAsync("fwdm_start").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("fwdm_stop").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ForwardToAll()
            {
                var enabled = _service.ForwardToAll();

                if (enabled)
                    await ReplyConfirmLocalizedAsync("fwall_start").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("fwall_stop").ConfigureAwait(false);

            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ShardStats(int page = 1)
            {
                if (--page < 0)
                    return;

                var statuses = _service.GetAllShardStatuses();

                var status = string.Join(", ", statuses
                    .GroupBy(x => x.ConnectionState)
                    .Select(x => $"{x.Count()} {x.Key}")
                    .ToArray());

                var allShardStrings = statuses
                    .Select(x =>
                    {
                        var timeDiff = DateTime.UtcNow - x.Time;
                        if (timeDiff >= TimeSpan.FromSeconds(30))
                            return $"Shard #{Format.Bold(x.ShardId.ToString())} **UNRESPONSIVE** for {timeDiff.ToString(@"hh\:mm\:ss")}";
                        return GetText("shard_stats_txt", x.ShardId.ToString(),
                            Format.Bold(x.ConnectionState.ToString()), Format.Bold(x.Guilds.ToString()), timeDiff.ToString(@"hh\:mm\:ss"));
                    })
                    .ToArray();

                await ctx.SendPaginatedConfirmAsync(page, (curPage) =>
                {

                    var str = string.Join("\n", allShardStrings.Skip(25 * curPage).Take(25));

                    if (string.IsNullOrWhiteSpace(str))
                        str = GetText("no_shards_on_page");

                    return new EmbedBuilder()
                        .WithAuthor(a => a.WithName(GetText("shard_stats")))
                        .WithTitle(status)
                        .WithOkColor()
                        .WithDescription(str);
                }, allShardStrings.Length, 25).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task RestartShard(int shardId)
            {
                var success = _service.RestartShard(shardId);
                if (success)
                {
                    await ReplyConfirmLocalizedAsync("shard_reconnecting", Format.Bold("#" + shardId)).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalizedAsync("no_shard_id").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task Leave([Leftover] string guildStr)
            {
                return _service.LeaveGuild(guildStr);
            }


            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task Die()
            {
                try
                {
                    await ReplyConfirmLocalizedAsync("shutting_down").ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
                await Task.Delay(2000).ConfigureAwait(false);
                _service.Die();
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task Restart()
            {
                bool success = _service.RestartBot();
                if (!success)
                {
                    await ReplyErrorLocalizedAsync("restart_fail").ConfigureAwait(false);
                    return;
                }

                try { await ReplyConfirmLocalizedAsync("restarting").ConfigureAwait(false); } catch { }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task SetName([Leftover] string newName)
            {
                if (string.IsNullOrWhiteSpace(newName))
                    return;

                try
                {
                    await _client.CurrentUser.ModifyAsync(u => u.Username = newName).ConfigureAwait(false);
                }
                catch (RateLimitedException)
                {
                    _log.Warn("You've been ratelimited. Wait 2 hours to change your name.");
                }

                await ReplyConfirmLocalizedAsync("bot_name", Format.Bold(newName)).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [UserPerm(GuildPerm.ManageNicknames)]
            [BotPerm(GuildPerm.ChangeNickname)]
            [Priority(0)]
            public async Task SetNick([Leftover] string newNick = null)
            {
                if (string.IsNullOrWhiteSpace(newNick))
                    return;
                var curUser = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
                await curUser.ModifyAsync(u => u.Nickname = newNick).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("bot_nick", Format.Bold(newNick) ?? "-").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [BotPerm(GuildPerm.ManageNicknames)]
            [UserPerm(GuildPerm.ManageNicknames)]
            [Priority(1)]
            public async Task SetNick(IGuildUser gu, [Leftover] string newNick = null)
            {
                var sg = (SocketGuild) Context.Guild;
                if (sg.OwnerId == gu.Id ||
                    gu.GetRoles().Max(r => r.Position) >= sg.CurrentUser.GetRoles().Max(r => r.Position))
                {
                    await ReplyErrorLocalizedAsync("insuf_perms_i");
                    return;
                }
                
                await gu.ModifyAsync(u => u.Nickname = newNick).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("user_nick", Format.Bold(gu.ToString()), Format.Bold(newNick) ?? "-").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task SetStatus([Leftover] SettableUserStatus status)
            {
                await _client.SetStatusAsync(SettableUserStatusToUserStatus(status)).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("bot_status", Format.Bold(status.ToString())).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task SetAvatar([Leftover] string img = null)
            {
                var success = await _service.SetAvatar(img);

                if (success)
                {
                    await ReplyConfirmLocalizedAsync("set_avatar").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task SetGame(ActivityType type, [Leftover] string game = null)
            {
                var rep = new ReplacementBuilder()
                    .WithDefault(Context)
                    .Build();

                await _bot.SetGameAsync(game == null ? game : rep.Replace(game), type).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("set_game").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task SetStream(string url, [Leftover] string name = null)
            {
                name = name ?? "";

                await _client.SetGameAsync(name, url, ActivityType.Streaming).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("set_stream").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task Send(string where, [Leftover] string msg = null)
            {
                if (string.IsNullOrWhiteSpace(msg))
                    return;

                var ids = where.Split('|');
                if (ids.Length != 2)
                    return;
                var sid = ulong.Parse(ids[0]);
                var server = _client.Guilds.FirstOrDefault(s => s.Id == sid);

                if (server == null)
                    return;

                var rep = new ReplacementBuilder()
                    .WithDefault(Context)
                    .Build();

                if (ids[1].ToUpperInvariant().StartsWith("C:", StringComparison.InvariantCulture))
                {
                    var cid = ulong.Parse(ids[1].Substring(2));
                    var ch = server.TextChannels.FirstOrDefault(c => c.Id == cid);
                    if (ch == null)
                    {
                        return;
                    }

                    if (CREmbed.TryParse(msg, out var crembed))
                    {
                        rep.Replace(crembed);
                        await ch.EmbedAsync(crembed).ConfigureAwait(false);
                        await ReplyConfirmLocalizedAsync("message_sent").ConfigureAwait(false);
                        return;
                    }
                    await ch.SendMessageAsync(rep.Replace(msg).SanitizeMentions()).ConfigureAwait(false);
                }
                else if (ids[1].ToUpperInvariant().StartsWith("U:", StringComparison.InvariantCulture))
                {
                    var uid = ulong.Parse(ids[1].Substring(2));
                    var user = server.Users.FirstOrDefault(u => u.Id == uid);
                    if (user == null)
                    {
                        return;
                    }

                    if (CREmbed.TryParse(msg, out var crembed))
                    {
                        rep.Replace(crembed);
                        await (await user.GetOrCreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(crembed)
                            .ConfigureAwait(false);
                        await ReplyConfirmLocalizedAsync("message_sent").ConfigureAwait(false);
                        return;
                    }

                    await (await user.GetOrCreateDMChannelAsync().ConfigureAwait(false)).SendMessageAsync(rep.Replace(msg).SanitizeMentions()).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalizedAsync("invalid_format").ConfigureAwait(false);
                    return;
                }
                await ReplyConfirmLocalizedAsync("message_sent").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ImagesReload()
            {
                _service.ReloadImages();
                await ReplyConfirmLocalizedAsync("images_loading", 0).ConfigureAwait(false);
            }
            
            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task StringsReload()
            {
                // todo add publisher to this. something like v3 eventpusher
                _strings.Reload();
                await ReplyConfirmLocalizedAsync("bot_strings_reloaded").ConfigureAwait(false);
            }

            private static UserStatus SettableUserStatusToUserStatus(SettableUserStatus sus)
            {
                switch (sus)
                {
                    case SettableUserStatus.Online:
                        return UserStatus.Online;
                    case SettableUserStatus.Invisible:
                        return UserStatus.Invisible;
                    case SettableUserStatus.Idle:
                        return UserStatus.AFK;
                    case SettableUserStatus.Dnd:
                        return UserStatus.DoNotDisturb;
                }

                return UserStatus.Online;
            }

            public enum SettableUserStatus
            {
                Online,
                Invisible,
                Idle,
                Dnd
            }
        }
    }
}