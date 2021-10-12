using Discord.WebSocket;
using Mewdeko.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Mewdeko._Extensions;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;
using Mewdeko.Common.Replacements;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Serilog;

namespace Mewdeko.Modules.OwnerOnly
{
    public class OwnerOnly : MewdekoModule<OwnerOnlyService>
    {
        public enum SettableUserStatus
        {
            Online,
            Invisible,
            Idle,
            Dnd
        }

        private readonly Mewdeko.Services.Mewdeko _bot;
        private readonly IBotStrings _strings;
        private readonly InteractiveService Interactivity;
        private readonly ICoordinator _coord;
        private readonly DiscordSocketClient _client;
        private readonly IEnumerable<IConfigService> _settingServices;


        public OwnerOnly(DiscordSocketClient client, Mewdeko.Services.Mewdeko bot, IBotStrings strings, InteractiveService serv, ICoordinator coord, IEnumerable<IConfigService> settingServices)
        {
            Interactivity = serv;
            _client = client;
            _bot = bot;
            _strings = strings;
            _coord = coord;
            _settingServices = settingServices;
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public async Task Config(string name = null, string prop = null, [Remainder] string value = null)
        {
            var configNames = _settingServices.Select(x => x.Name);

            // if name is not provided, print available configs
            name = name?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name))
            {
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(GetText("config_list"))
                    .WithDescription(string.Join("\n", configNames));

                await ctx.Channel.EmbedAsync(embed);
                return;
            }

            var setting = _settingServices.FirstOrDefault(x =>
                x.Name.StartsWith(name, StringComparison.InvariantCultureIgnoreCase));

            // if config name is not found, print error and the list of configs
            if (setting is null)
            {
                var embed = new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(GetText("config_not_found", Format.Code(name)))
                    .AddField(GetText("config_list"), string.Join("\n", configNames));

                await ctx.Channel.EmbedAsync(embed);
                return;
            }

            name = setting.Name;

            // if prop is not sent, then print the list of all props and values in that config
            prop = prop?.ToLowerInvariant();
            var propNames = setting.GetSettableProps();
            if (string.IsNullOrWhiteSpace(prop))
            {
                var propStrings = GetPropsAndValuesString(setting, propNames);
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"⚙️ {setting.Name}")
                    .WithDescription(propStrings);


                await ctx.Channel.EmbedAsync(embed);
                return;
            }
            // if the prop is invalid -> print error and list of 

            var exists = propNames.Any(x => x == prop);

            if (!exists)
            {
                var propStrings = GetPropsAndValuesString(setting, propNames);
                var propErrorEmbed = new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(GetText("config_prop_not_found", Format.Code(prop), Format.Code(name)))
                    .AddField($"⚙️ {setting.Name}", propStrings);

                await ctx.Channel.EmbedAsync(propErrorEmbed);
                return;
            }

            // if prop is sent, but value is not, then we have to check
            // if prop is valid -> 
            if (string.IsNullOrWhiteSpace(value))
            {
                value = setting.GetSetting(prop);
                if (prop != "currency.sign") Format.Code(Format.Sanitize(value?.TrimTo(1000)), "json");

                if (string.IsNullOrWhiteSpace(value))
                    value = "-";

                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .AddField("Config", Format.Code(setting.Name), true)
                    .AddField("Prop", Format.Code(prop), true)
                    .AddField("Value", value);

                var comment = setting.GetComment(prop);
                if (!string.IsNullOrWhiteSpace(comment))
                    embed.AddField("Comment", comment);

                await ctx.Channel.EmbedAsync(embed);
                return;
            }

            var success = setting.SetSetting(prop, value);

            if (!success)
            {
                await ReplyErrorLocalizedAsync("config_edit_fail", Format.Code(prop), Format.Code(value));
                return;
            }

            await ctx.OkAsync();
        }

        private string GetPropsAndValuesString(IConfigService config, IEnumerable<string> names)
        {
            var propValues = names.Select(pr =>
            {
                var val = config.GetSetting(pr);
                if (pr != "currency.sign")
                    val = val?.TrimTo(28);
                return val?.Replace("\n", "") ?? "-";
            });

            var strings = names.Zip(propValues, (name, value) =>
                $"{name,-25} = {value}\n");

            return Format.Code(string.Concat(strings), "hs");
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public async Task RotatePlaying()
        {
            if (_service.ToggleRotatePlaying())
                await ReplyConfirmLocalizedAsync("ropl_enabled").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("ropl_disabled").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public async Task AddPlaying(ActivityType t, [Remainder] string status)
        {
            await _service.AddPlaying(t, status).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("ropl_added").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public async Task ListPlaying()
        {
            var statuses = _service.GetRotatingStatuses();

            if (!statuses.Any())
            {
                await ReplyErrorLocalizedAsync("ropl_not_set").ConfigureAwait(false);
            }
            else
            {
                var i = 1;
                await ReplyConfirmLocalizedAsync("ropl_list",
                        string.Join("\n\t", statuses.Select(rs => $"`{i++}.` *{rs.Type}* {rs.Status}")))
                    .ConfigureAwait(false);
            }
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public async Task DefPrefix([Remainder] string prefix = null)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                await ReplyConfirmLocalizedAsync("defprefix_current", CmdHandler.GetPrefix()).ConfigureAwait(false);
                return;
            }

            var oldPrefix = CmdHandler.GetPrefix();
            var newPrefix = CmdHandler.SetDefaultPrefix(prefix);

            await ReplyConfirmLocalizedAsync("defprefix_new", Format.Code(oldPrefix), Format.Code(newPrefix))
                .ConfigureAwait(false);
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public async Task RemovePlaying(int index)
        {
            index -= 1;

            var msg = await _service.RemovePlayingAsync(index).ConfigureAwait(false);

            if (msg == null)
                return;

            await ReplyConfirmLocalizedAsync("reprm", msg).ConfigureAwait(false);
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public async Task LanguageSetDefault(string name)
        {
            try
            {
                CultureInfo ci;
                if (name.Trim().ToLowerInvariant() == "default")
                {
                    Localization.ResetDefaultCulture();
                    ci = Localization.DefaultCultureInfo;
                }
                else
                {
                    ci = new CultureInfo(name);
                    Localization.SetDefaultCulture(ci);
                }

                await ReplyConfirmLocalizedAsync("lang_set_bot", Format.Bold(ci.ToString()),
                    Format.Bold(ci.NativeName)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await ReplyErrorLocalizedAsync("lang_set_fail").ConfigureAwait(false);
            }
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
                        .WithColor(Mewdeko.Services.Mewdeko.OkColor)
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
            var server = _client.Rest.GetGuildAsync(sid).Result;

            if (server == null)
                return;

            var rep = new ReplacementBuilder()
                .WithDefault(Context)
                .Build();

            if (ids[1].ToUpperInvariant().StartsWith("C:", StringComparison.InvariantCulture))
            {
                var cid = ulong.Parse(ids[1].Substring(2));
                var ch = server.GetTextChannelsAsync().Result.FirstOrDefault(c => c.Id == cid);
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
                var user = server.GetUsersAsync().FlattenAsync().Result.FirstOrDefault(u => u.Id == uid);
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
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public async Task Bash([Remainder] string message)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{message} 2>&1\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (ctx.Channel.EnterTypingState())
                {
                    process.Start();

                    // Synchronously read the standard output of the spawned process.
                    var reader = process.StandardOutput;

                    var output = await reader.ReadToEndAsync();
                    if (output.Length > 2000)
                    {
                        var chunkSize = 1988;
                        var stringLength = output.Length;
                        for (var i = 0; i < stringLength; i += chunkSize)
                        {
                            if (i + chunkSize > stringLength) chunkSize = stringLength - i;
                            await ctx.Channel.SendMessageAsync($"```bash\n{output.Substring(i, chunkSize)}```");
                            await process.WaitForExitAsync();
                        }
                    }
                    else if (output == "")
                    {
                        await ctx.Channel.SendMessageAsync("```The output was blank```");
                    }
                    else
                    {
                        await ctx.Channel.SendMessageAsync($"```bash\n{output}```");
                    }
                }

                await process.WaitForExitAsync();
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [OwnerOnly]
        public async Task Evaluate([Remainder] string code)
        {
            var cs1 = code.IndexOf("```", StringComparison.Ordinal) + 3;
            cs1 = code.IndexOf('\n', cs1) + 1;
            var cs2 = code.LastIndexOf("```", StringComparison.Ordinal);

            if (cs1 == -1 || cs2 == -1)
                throw new ArgumentException("You need to wrap the code into a code block.", nameof(code));

            code = code.Substring(cs1, cs2 - cs1);

            var embed = new EmbedBuilder
            {
                Title = "Evaluating...",
                Color = new Color(0xD091B2)
            };
            var msg = await ctx.Channel.SendMessageAsync("", embed: embed.Build());

            var globals = new EvaluationEnvironment((CommandContext)Context);
            var sopts = ScriptOptions.Default
                .WithImports("System", "System.Collections.Generic", "System.Diagnostics", "System.Linq",
                    "System.Net.Http", "System.Net.Http.Headers", "System.Reflection", "System.Text",
                    "System.Threading.Tasks", "Discord.Net", "Discord", "Discord.WebSocket", "Mewdeko.Modules",
                    "Mewdeko.Services", "Mewdeko._Extensions", "Mewdeko.Modules.Administration",
                    "Mewdeko.Modules.CustomReactions", "Mewdeko.Modules.Gambling", "Mewdeko.Modules.Games",
                    "Mewdeko.Modules.Help", "Mewdeko.Modules.Music", "Mewdeko.Modules.Nsfw",
                    "Mewdeko.Modules.Permissions", "Mewdeko.Modules.Searches", "Mewdeko.Modules.Server_Management")
                .WithReferences(AppDomain.CurrentDomain.GetAssemblies()
                    .Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location)));

            var sw1 = Stopwatch.StartNew();
            var cs = CSharpScript.Create(code, sopts, typeof(EvaluationEnvironment));
            var csc = cs.Compile();
            sw1.Stop();

            if (csc.Any(xd => xd.Severity == DiagnosticSeverity.Error))
            {
                embed = new EmbedBuilder
                {
                    Title = "Compilation failed",
                    Description = string.Concat("Compilation failed after ",
                        sw1.ElapsedMilliseconds.ToString("#,##0"), "ms with ", csc.Length.ToString("#,##0"),
                        " errors."),
                    Color = new Color(0xD091B2)
                };
                foreach (var xd in csc.Take(3))
                {
                    var ls = xd.Location.GetLineSpan();
                    embed.AddField(
                        string.Concat("Error at ", ls.StartLinePosition.Line.ToString("#,##0"), ", ",
                            ls.StartLinePosition.Character.ToString("#,##0")), Format.Code(xd.GetMessage()));
                }

                if (csc.Length > 3)
                    embed.AddField("Some errors omitted",
                        string.Concat((csc.Length - 3).ToString("#,##0"), " more errors not displayed"));
                await msg.ModifyAsync(x => x.Embed = embed.Build());
                return;
            }

            Exception rex = null;
            ScriptState<object> css = null;
            var sw2 = Stopwatch.StartNew();
            try
            {
                css = await cs.RunAsync(globals);
                rex = css.Exception;
            }
            catch (Exception ex)
            {
                rex = ex;
            }

            sw2.Stop();

            if (rex != null)
            {
                embed = new EmbedBuilder
                {
                    Title = "Execution failed",
                    Description = string.Concat("Execution failed after ",
                        sw2.ElapsedMilliseconds.ToString("#,##0"), "ms with `", rex.GetType(), ": ", rex.Message,
                        "`."),
                    Color = new Color(0xD091B2)
                };
                await msg.ModifyAsync(x => { x.Embed = embed.Build(); });
                return;
            }

            // execution succeeded
            embed = new EmbedBuilder
            {
                Title = "Evaluation successful",
                Color = new Color(0xD091B2)
            };

            embed.AddField("Result", css.ReturnValue != null ? css.ReturnValue.ToString() : "No value returned")
                .AddField("Compilation time", string.Concat(sw1.ElapsedMilliseconds.ToString("#,##0"), "ms"), true)
                .AddField("Execution time", string.Concat(sw2.ElapsedMilliseconds.ToString("#,##0"), "ms"), true);

            if (css.ReturnValue != null)
                embed.AddField("Return type", css.ReturnValue.GetType().ToString(), true);

            await msg.ModifyAsync(x => { x.Embed = embed.Build(); });
        }


    }

    public sealed class EvaluationEnvironment
    {
        public EvaluationEnvironment(CommandContext ctx)
        {
            this.ctx = ctx;
        }

        public CommandContext ctx { get; }

        public IUserMessage Message => ctx.Message;
        public IMessageChannel Channel => ctx.Channel;
        public IGuild Guild => ctx.Guild;
        public IUser User => ctx.User;
        public IGuildUser Member => (IGuildUser)ctx.User;
        public DiscordSocketClient Client => ctx.Client as DiscordSocketClient;
    }
}
