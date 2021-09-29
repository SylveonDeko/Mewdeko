using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Humanizer;
using Mewdeko.Common;
using Mewdeko.Common.Replacements;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Extensions;

namespace Mewdeko.Modules.Utility.Services
{
    public class AFKService : INService
    {
        private readonly DbService _db;
        private readonly CommandHandler Cmd;
        public DiscordSocketClient _client;


        public AFKService(DbService db, DiscordSocketClient client, CommandHandler handle, Mewdeko bot)
        {
            _db = db;
            _client = client;
            Cmd = handle;
            _AfkType = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.AfkType)
                .ToConcurrent();
            _AfkTimeout = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.AfkTimeout)
                .ToConcurrent();
            _AfkDisabledChannels = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.AfkDisabledChannels)
                .ToConcurrent();
            _AfkLengths = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.AfkLength)
                .ToConcurrent();
            _AfkMessage = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.AfkMessage)
                .ToConcurrent();
            _client.MessageReceived += MessageReceived;
            _client.InteractionCreated += OnInteractionCreated;
            _client.MessageUpdated += MessageUpdated;
            _client.UserIsTyping += UserTyping;
        }

        private ConcurrentDictionary<ulong, int> _AfkType { get; } = new();
        private ConcurrentDictionary<ulong, string> _AfkMessage { get; } = new();
        private ConcurrentDictionary<ulong, int> _AfkTimeout { get; } = new();
        private ConcurrentDictionary<ulong, int> _AfkLengths { get; } = new();
        private ConcurrentDictionary<ulong, string> _AfkDisabledChannels { get; } = new();

        private async Task OnInteractionCreated(SocketInteraction interaction)
        {
            if (interaction is SocketSlashCommand command)
                switch (command.Data.Name)
                {
                    case "afk":
                        await HandleAfkSlashCommands(command);
                        break;
                }
            else
                return;
        }

        private Task HandleAfkSlashCommands(SocketSlashCommand command)
        {
            _ = Task.Run(async () =>
            {
                await command.DeferAsync();
                if (command.Channel is ITextChannel chan)
                {
                    if (command.Data.Options?.Count == 1)
                    {
                        await AFKSet(chan.Guild, command.User as IGuildUser,
                            command.Data.Options.FirstOrDefault().Value.ToString(), 0);
                        await command.RespondAsync(
                            $"I have succesfully enabled your afk and set it to {command.Data.Options.FirstOrDefault().Value.ToString().SanitizeAllMentions()}");
                        return;
                    }

                    if (IsAfk(chan.Guild, command.User as IGuildUser))
                    {
                        await AFKSet(chan.Guild, command.User as IGuildUser, "", 0);
                        try
                        {
                            await command.FollowupAsync("I have disabled your afk.");
                        }
                        catch (Exception ex)
                        {
                            Console.Write(ex);
                        }

                        var msg = await command.GetOriginalResponseAsync();
                        msg.DeleteAfter(5);
                    }
                    else
                    {
                        await AFKSet(chan.Guild, command.User as IGuildUser, "_ _", 0);
                        await command.FollowupAsync("I have enabled your afk!");
                        var msg = await command.GetOriginalResponseAsync();
                        msg.DeleteAfter(5);
                    }
                }
            });
            return Task.CompletedTask;
        }

        public Task UserTyping(Cacheable<IUser, ulong> user, Cacheable<IMessageChannel, ulong> chan)
        {
            _ = Task.Run(async () =>
            {
                if (user.Value is IGuildUser use)
                    if (GetAfkType(use.GuildId) == 2)
                        if (IsAfk(use.Guild, use))
                        {
                            var t = AfkMessage(use.Guild.Id, user.Id).Last();
                            if (t.DateAdded != null &&
                                t.DateAdded.Value.ToLocalTime() <
                                DateTime.Now.AddSeconds(-GetAfkTimeout(use.GuildId)) && t.WasTimed == 0)
                            {
                                await AFKSet(use.Guild, use, "", 0);
                                var msg = await chan.Value.SendMessageAsync(
                                    $"Welcome back {user.Value.Mention}! I noticed you typing so I disabled your afk.");
                                msg.DeleteAfter(5);
                            }
                        }
            });
            return Task.CompletedTask;
        }

        public Task MessageReceived(SocketMessage msg)
        {
            _ = Task.Run(async () =>
            {
                if (msg.Author is IGuildUser user)
                {
                    if (GetAfkType(user.Guild.Id) == 3)
                        if (IsAfk(user.Guild, user))
                        {
                            var t = AfkMessage(user.Guild.Id, user.Id).Last();
                            if (t.DateAdded != null &&
                                t.DateAdded.Value.ToLocalTime() <
                                DateTime.Now.AddSeconds(-GetAfkTimeout(user.GuildId)) && t.WasTimed == 0)
                            {
                                await AFKSet(user.Guild, user, "", 0);
                                var ms = await msg.Channel.SendMessageAsync(
                                    $"Welcome back {user.Mention}, I have disabled your AFK for you.");
                                ms.DeleteAfter(5);
                                return;
                            }
                        }

                    if (msg.MentionedUsers.Count > 0 && !msg.Author.IsBot)
                    {
                        if (msg.Content.Contains($"{Cmd.GetPrefix(user.Guild)}afkremove") ||
                            msg.Content.Contains($"{Cmd.GetPrefix(user.Guild)}afkrm") ||
                            msg.Content.Contains($"{Cmd.GetPrefix(user.Guild)}afk")) return;
                        var IDs = msg.MentionedUsers;
                        if (GetDisabledAfkChannels(user.GuildId) != "0" && GetDisabledAfkChannels(user.GuildId) != null)
                        {
                            var chans = GetDisabledAfkChannels(user.GuildId);
                            var e = chans.Split(",");
                            if (e.Contains(msg.Channel.Id.ToString())) return;
                        }

                        if (msg.MentionedUsers.FirstOrDefault() is not IGuildUser mentuser) return;
                        if (IsAfk(user.Guild, mentuser))
                        {
                            CREmbed crEmbed = null;
                            var replacer = new ReplacementBuilder()
                                .WithOverride("%afk.message%",
                                    () => AfkMessage(user.GuildId, mentuser.Id).Last().Message
                                        .Truncate(GetAfkLength(user.GuildId)))
                                .WithOverride("%afk.user%", () => mentuser.ToString())
                                .WithOverride("%afk.user.mention%", () => mentuser.Mention)
                                .WithOverride("%afk.user.avatar%", () => mentuser.GetAvatarUrl(size: 2048))
                                .WithOverride("%afk.user.id%", () => mentuser.Id.ToString())
                                .WithOverride("%afk.triggeruser%", () => msg.Author.ToString())
                                .WithOverride("%afk.triggeruser.avatar%", () => msg.Author.RealAvatarUrl().ToString())
                                .WithOverride("%afk.triggeruser.id%", () => msg.Author.Id.ToString())
                                .WithOverride("%afk.triggeruser.mention%", () => msg.Author.Mention)
                                .WithOverride("%afk.time%",
                                    () =>
                                        $"{(DateTime.UtcNow - AfkMessage(user.GuildId, user.Id).Last().DateAdded.Value).Humanize()}")
                                .Build();
                            var ebe = CREmbed.TryParse(GetCustomAfkMessage(mentuser.GuildId), out crEmbed);
                            if (!ebe)
                            {
                                await SetCustomAfkMessage(user.Guild, "-");
                                await msg.Channel.EmbedAsync(new EmbedBuilder()
                                    .WithAuthor(eab => eab.WithName($"{mentuser} is currently away")
                                        .WithIconUrl(mentuser.GetAvatarUrl()))
                                    .WithDescription(AfkMessage(user.GuildId, mentuser.Id).Last().Message
                                        .Truncate(GetAfkLength(user.Guild.Id)))
                                    .WithFooter(new EmbedFooterBuilder
                                    {
                                        Text =
                                            $"AFK for {(DateTime.UtcNow - AfkMessage(user.GuildId, mentuser.Id).Last().DateAdded.Value).Humanize()}"
                                    })
                                    .WithOkColor());
                                return;
                            }

                            replacer.Replace(crEmbed);
                            if (crEmbed.PlainText != null && crEmbed.IsEmbedValid)
                            {
                                await msg.Channel.SendMessageAsync(crEmbed.PlainText.SanitizeAllMentions(),
                                    embed: crEmbed.ToEmbed().Build());
                                return;
                            }

                            if (crEmbed.PlainText is null)
                            {
                                await msg.Channel.SendMessageAsync(embed: crEmbed.ToEmbed().Build());
                                return;
                            }

                            if (crEmbed.PlainText != null && !crEmbed.IsEmbedValid)
                                await msg.Channel.SendMessageAsync(crEmbed.PlainText.SanitizeAllMentions());
                        }
                    }
                }
            });
            return Task.CompletedTask;
        }

        public async Task SetCustomAfkMessage(IGuild guild, string AfkMessage)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.AfkMessage = AfkMessage;
                await uow.SaveChangesAsync();
            }

            _AfkMessage.AddOrUpdate(guild.Id, AfkMessage, (key, old) => AfkMessage);
        }

        public async Task TimedAfk(IGuild guild, IUser user, string message, TimeSpan time)
        {
            await AFKSet(guild, user as IGuildUser, message, 1);
            Thread.Sleep(time);
            await AFKSet(guild, user as IGuildUser, "", 0);
        }

        public bool IsAfk(IGuild guild, IGuildUser user)
        {
            var afkmsg = AfkMessage(guild.Id, user.Id).Last();
            return afkmsg.Message != "";
        }

        public Task MessageUpdated(Cacheable<IMessage, ulong> msg, SocketMessage msg2, ISocketMessageChannel t)
        {
            if (msg.Value is not null && msg.Value.Content == msg2.Content) return Task.CompletedTask;
            return MessageReceived(msg2);
        }

        public async Task AfkTypeSet(IGuild guild, int num)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.AfkType = num;
                await uow.SaveChangesAsync();
            }

            _AfkType.AddOrUpdate(guild.Id, num, (key, old) => num);
        }

        public async Task AfkLengthSet(IGuild guild, int num)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.AfkLength = num;
                await uow.SaveChangesAsync();
            }

            _AfkLengths.AddOrUpdate(guild.Id, num, (key, old) => num);
        }


        public async Task AfkTimeoutSet(IGuild guild, int num)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.AfkTimeout = num;
                await uow.SaveChangesAsync();
            }

            _AfkTimeout.AddOrUpdate(guild.Id, num, (key, old) => num);
        }

        public async Task AfkDisabledSet(IGuild guild, string num)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.AfkDisabledChannels = num;
                await uow.SaveChangesAsync();
            }

            _AfkDisabledChannels.AddOrUpdate(guild.Id, num, (key, old) => num);
        }

        public string GetCustomAfkMessage(ulong? id)
        {
            _AfkMessage.TryGetValue(id.Value, out var snum);
            return snum;
        }

        public int GetAfkType(ulong? id)
        {
            _AfkType.TryGetValue(id.Value, out var snum);
            return snum;
        }

        public int GetAfkLength(ulong? id)
        {
            _AfkLengths.TryGetValue(id.Value, out var snum);
            return snum;
        }

        public string GetDisabledAfkChannels(ulong? id)
        {
            _AfkDisabledChannels.TryGetValue(id.Value, out var snum);
            return snum;
        }

        public int GetAfkTimeout(ulong? id)
        {
            _AfkTimeout.TryGetValue(id.Value, out var snum);
            return snum;
        }

        public async Task AFKSet(IGuild guild, IGuildUser user, string message, int timed)
        {
            var aFK = new AFK
            {
                GuildId = guild.Id,
                UserId = user.Id,
                Message = message,
                WasTimed = timed
            };
            var afk = aFK;
            using var uow = _db.GetDbContext();
            uow.AFK.Update(afk);
            await uow.SaveChangesAsync();
        }

        public AFK[] AfkMessage(ulong gid, ulong uid)
        {
            using var uow = _db.GetDbContext();
            return uow.AFK.ForId(gid, uid);
        }
    }
}