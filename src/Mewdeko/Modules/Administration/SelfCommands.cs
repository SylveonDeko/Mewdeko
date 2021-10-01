using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Replacements;
using Mewdeko.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Interactive;
using Mewdeko.Interactive.Pagination;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Services;
using Serilog;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class SelfCommands : MewdekoSubmodule<SelfService>
        {
            public enum SettableUserStatus
            {
                Online,
                Invisible,
                Idle,
                Dnd
            }

            private readonly Mewdeko _bot;
            private readonly DiscordSocketClient _client;
            private readonly IBotStrings _strings;
            private readonly InteractiveService Interactivity;
            private readonly ICoordinator _coord;

            public SelfCommands(DiscordSocketClient client, Mewdeko bot, IBotStrings strings, InteractiveService serv, ICoordinator coord)
            {
                Interactivity = serv;
                _client = client;
                _bot = bot;
                _strings = strings;
                _coord = coord;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [OwnerOnly]
            public async Task StartupCommandAdd([Remainder] string cmdText)
            {
                if (cmdText.StartsWith(Prefix + "die", StringComparison.InvariantCulture))
                    return;

                var guser = (IGuildUser)ctx.User;
                var cmd = new AutoCommand
                {
                    CommandText = cmdText,
                    ChannelId = ctx.Channel.Id,
                    ChannelName = ctx.Channel.Name,
                    GuildId = ctx.Guild?.Id,
                    GuildName = ctx.Guild?.Name,
                    VoiceChannelId = guser.VoiceChannel?.Id,
                    VoiceChannelName = guser.VoiceChannel?.Name,
                    Interval = 0
                };
                _service.AddNewAutoCommand(cmd);

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle(GetText("scadd"))
                    .AddField(efb => efb.WithName(GetText("server"))
                        .WithValue(cmd.GuildId == null ? "-" : $"{cmd.GuildName}/{cmd.GuildId}").WithIsInline(true))
                    .AddField(efb => efb.WithName(GetText("channel"))
                        .WithValue($"{cmd.ChannelName}/{cmd.ChannelId}").WithIsInline(true))
                    .AddField(efb => efb.WithName(GetText("command_text"))
                        .WithValue(cmdText).WithIsInline(false))).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [OwnerOnly]
            public async Task AutoCommandAdd(int interval, [Remainder] string cmdText)
            {
                if (cmdText.StartsWith(Prefix + "die", StringComparison.InvariantCulture))
                    return;

                if (interval < 5)
                    return;

                var guser = (IGuildUser)ctx.User;
                var cmd = new AutoCommand
                {
                    CommandText = cmdText,
                    ChannelId = ctx.Channel.Id,
                    ChannelName = ctx.Channel.Name,
                    GuildId = ctx.Guild?.Id,
                    GuildName = ctx.Guild?.Name,
                    VoiceChannelId = guser.VoiceChannel?.Id,
                    VoiceChannelName = guser.VoiceChannel?.Name,
                    Interval = interval
                };
                _service.AddNewAutoCommand(cmd);

                await ReplyConfirmLocalizedAsync("autocmd_add", Format.Code(Format.Sanitize(cmdText)), cmd.Interval)
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [OwnerOnly]
            public async Task StartupCommandsList(int page = 1)
            {
                if (page-- < 1)
                    return;

                var scmds = _service.GetStartupCommands()
                    .Skip(page * 5)
                    .Take(5)
                    .ToList();

                if (scmds.Count == 0)
                {
                    await ReplyErrorLocalizedAsync("startcmdlist_none").ConfigureAwait(false);
                }
                else
                {
                    var i = 0;
                    await ctx.Channel.SendConfirmAsync(
                            text: string.Join("\n", scmds
                                .Select(x => $@"```css
#{++i}
[{GetText("server")}]: {(x.GuildId.HasValue ? $"{x.GuildName} #{x.GuildId}" : "-")}
[{GetText("channel")}]: {x.ChannelName} #{x.ChannelId}
[{GetText("command_text")}]: {x.CommandText}```")),
                            title: string.Empty,
                            footer: GetText("page", page + 1))
                        .ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [OwnerOnly]
            public async Task AutoCommandsList(int page = 1)
            {
                if (page-- < 1)
                    return;

                var scmds = _service.GetAutoCommands()
                    .Skip(page * 5)
                    .Take(5)
                    .ToList();
                if (!scmds.Any())
                {
                    await ReplyErrorLocalizedAsync("autocmdlist_none").ConfigureAwait(false);
                }
                else
                {
                    var i = 0;
                    await ctx.Channel.SendConfirmAsync(
                            text: string.Join("\n", scmds
                                .Select(x => $@"```css
#{++i}
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

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
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
                catch
                {
                }

                await Task.Delay(miliseconds).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [OwnerOnly]
            public async Task AutoCommandRemove([Remainder] int index)
            {
                if (!_service.RemoveAutoCommand(--index, out _))
                {
                    await ReplyErrorLocalizedAsync("acrm_fail").ConfigureAwait(false);
                    return;
                }

                await ctx.OkAsync();
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [OwnerOnly]
            public async Task StartupCommandRemove([Remainder] int index)
            {
                if (!_service.RemoveStartupCommand(--index, out _))
                    await ReplyErrorLocalizedAsync("scrm_fail").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("scrm").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [OwnerOnly]
            public async Task StartupCommandsClear()
            {
                _service.ClearStartupCommands();

                await ReplyConfirmLocalizedAsync("startcmds_cleared").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task ForwardMessages()
            {
                var enabled = _service.ForwardMessages();

                if (enabled)
                    await ReplyConfirmLocalizedAsync("fwdm_start").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("fwdm_stop").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task ForwardToAll()
            {
                var enabled = _service.ForwardToAll();

                if (enabled)
                    await ReplyConfirmLocalizedAsync("fwall_start").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("fwall_stop").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task ShardStats(int page = 1)
            {
                if (--page < 0)
                    return;

                var statuses = _coord.GetAllShardStatuses();

                var status = string.Join(" : ", statuses
                    .Select(x => (ConnectionStateToEmoji(x), x))
                    .GroupBy(x => x.Item1)
                    .Select(x => $"`{x.Count()} {x.Key}`")
                    .ToArray());

                var allShardStrings = statuses
                    .Select(st =>
                    {
                        var stateStr = ConnectionStateToEmoji(st);
                        var timeDiff = DateTime.UtcNow - st.LastUpdate;
                        var maxGuildCountLength = statuses.Max(x => x.GuildCount).ToString().Length;
                        return $"`{stateStr} " +
                               $"| #{st.ShardId.ToString().PadBoth(3)} " +
                               $"| {timeDiff:mm\\:ss} " +
                               $"| {st.GuildCount.ToString().PadBoth(maxGuildCountLength)} `";
                    })
                    .ToArray();



                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(allShardStrings.Length - 1)
                    .WithDefaultEmotes()
                    .Build();

                await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

                Task<PageBuilder> PageFactory(int page)
                {
                    {
                        var str = string.Join("\n", allShardStrings.Skip(25 * page).Take(25));

                        if (string.IsNullOrWhiteSpace(str))
                            str = GetText("no_shards_on_page");

                        return Task.FromResult(new PageBuilder()
                            .WithAuthor(a => a.WithName(GetText("shard_stats")))
                            .WithTitle(status)
                            .WithColor(Mewdeko.OkColor)
                            .WithDescription(str));
                    }
                }
            }
            private static string ConnectionStateToEmoji(ShardStatus status)
            {
                var timeDiff = DateTime.UtcNow - status.LastUpdate;
                return status.ConnectionState switch
                {
                    ConnectionState.Connected => "✅",
                    ConnectionState.Disconnected => "🔻",
                    _ when timeDiff > TimeSpan.FromSeconds(30) => " ❗ ",
                    _ => " ⏳"
                };
            }


            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task RestartShard(int shardId)
            {
                var success = _coord.RestartShard(shardId);
                if (success)
                {
                    await ReplyConfirmLocalizedAsync("shard_reconnecting", Format.Bold("#" + shardId)).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalizedAsync("no_shard_id").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public Task Leave([Remainder] string guildStr)
            {
                return _service.LeaveGuild(guildStr);
            }


            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
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
                _coord.Die();
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task Restart()
            {
                bool success = _coord.RestartBot();
                if (!success)
                {
                    await ReplyErrorLocalizedAsync("restart_fail").ConfigureAwait(false);
                    return;
                }

                try
                {
                    await ReplyConfirmLocalizedAsync("restarting").ConfigureAwait(false);
                }
                catch
                {
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task SetName([Remainder] string newName)
            {
                if (string.IsNullOrWhiteSpace(newName))
                    return;

                try
                {
                    await _client.CurrentUser.ModifyAsync(u => u.Username = newName).ConfigureAwait(false);
                }
                catch (RateLimitedException)
                {
                    Log.Warning("You've been ratelimited. Wait 2 hours to change your name");
                }

                await ReplyConfirmLocalizedAsync("bot_name", Format.Bold(newName)).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.ManageNicknames)]
            [BotPerm(GuildPerm.ChangeNickname)]
            [Priority(0)]
            public async Task SetNick([Remainder] string newNick = null)
            {
                if (string.IsNullOrWhiteSpace(newNick))
                    return;
                var curUser = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
                await curUser.ModifyAsync(u => u.Nickname = newNick).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("bot_nick", Format.Bold(newNick) ?? "-").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [BotPerm(GuildPerm.ManageNicknames)]
            [UserPerm(GuildPerm.ManageNicknames)]
            [Priority(1)]
            public async Task SetNick(IGuildUser gu, [Remainder] string newNick = null)
            {
                var sg = (SocketGuild)Context.Guild;
                if (sg.OwnerId == gu.Id ||
                    gu.GetRoles().Max(r => r.Position) >= sg.CurrentUser.GetRoles().Max(r => r.Position))
                {
                    await ReplyErrorLocalizedAsync("insuf_perms_i");
                    return;
                }

                await gu.ModifyAsync(u => u.Nickname = newNick).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("user_nick", Format.Bold(gu.ToString()), Format.Bold(newNick) ?? "-")
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task SetStatus([Remainder] SettableUserStatus status)
            {
                await _client.SetStatusAsync(SettableUserStatusToUserStatus(status)).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("bot_status", Format.Bold(status.ToString())).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task SetAvatar([Remainder] string img = null)
            {
                var success = await _service.SetAvatar(img);

                if (success) await ReplyConfirmLocalizedAsync("set_avatar").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task SetGame(ActivityType type, [Remainder] string game = null)
            {
                var rep = new ReplacementBuilder()
                    .WithDefault(Context)
                    .Build();

                await _bot.SetGameAsync(game == null ? game : rep.Replace(game), type).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("set_game").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task SetStream(string url, [Remainder] string name = null)
            {
                name = name ?? "";

                await _client.SetGameAsync(name, url, ActivityType.Streaming).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("set_stream").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task Send(string where, [Remainder] string msg = null)
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
                    if (ch == null) return;

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
                    if (user == null) return;

                    if (CREmbed.TryParse(msg, out var crembed))
                    {
                        rep.Replace(crembed);
                        await (await user.CreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(crembed)
                            .ConfigureAwait(false);
                        await ReplyConfirmLocalizedAsync("message_sent").ConfigureAwait(false);
                        return;
                    }

                    await (await user.CreateDMChannelAsync().ConfigureAwait(false))
                        .SendMessageAsync(rep.Replace(msg).SanitizeMentions()).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalizedAsync("invalid_format").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalizedAsync("message_sent").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task ImagesReload()
            {
                _service.ReloadImages();
                await ReplyConfirmLocalizedAsync("images_loading", 0).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task StringsReload()
            {
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
        }
    }
}