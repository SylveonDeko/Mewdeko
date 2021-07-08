using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
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
            _client.MessageReceived += MessageReceived;
            _client.MessageUpdated += MessageUpdated;
            _client.UserIsTyping += UserTyping;
        }

        private ConcurrentDictionary<ulong, int> _AfkType { get; } = new();
        private ConcurrentDictionary<ulong, int> _AfkTimeout { get; } = new();
        private ConcurrentDictionary<ulong, string> _AfkDisabledChannels { get; } = new();

        public Task UserTyping(Cacheable<IUser, ulong> user, Cacheable<IMessageChannel, ulong> chan)
        {
            _ = Task.Run(async () =>
            {
                if (user.Value is IGuildUser use)
                    if (GetAfkType(use.GuildId) == 2)
                        if (IsAfk(use.Guild, use))
                        {
                            var t = AfkMessage(use.Guild.Id, user.Id).Last();
                            if (t.DateAdded.Value.ToLocalTime() < DateTime.Now.AddSeconds(-GetAfkTimeout(use.GuildId)))
                            {
                                await AFKSet(use.Guild, use, "");
                                await chan.Value.SendMessageAsync(
                                    $"Welcome back {user.Value.Mention}! I noticed you typing so I disabled your afk.");
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
                            if (t.DateAdded.Value.ToLocalTime() < DateTime.Now.AddSeconds(-GetAfkTimeout(user.GuildId)))
                            {
                                await AFKSet(user.Guild, user, "");
                                await msg.Channel.SendMessageAsync(
                                    $"Welcome back {user.Mention}, I have disabled your AFK for you.");
                                return;
                            }
                        }

                    if (msg.MentionedUsers.Count > 0 && !msg.Author.IsBot)
                    {
                        if (msg.Content.Contains($"{Cmd.GetPrefix(user.Guild)}afkremove") ||
                            msg.Content.Contains($"{Cmd.GetPrefix(user.Guild)}afkremove")) return;
                        var IDs = msg.MentionedUsers;
                        if (GetDisabledAfkChannels(user.GuildId) != "0" && GetDisabledAfkChannels(user.GuildId) != null)
                        {
                            var chans = GetDisabledAfkChannels(user.GuildId);
                            var e = chans.Split(",");
                            if (e.Contains(msg.Channel.Id.ToString())) return;
                        }

                        foreach (var i in IDs)
                        {
                            var afkmsg = AfkMessage(user.Guild.Id, i.Id).Select(x => x.Message)
                                .Last();
                            if (afkmsg == "") return;
                            await ((ITextChannel) msg.Channel).EmbedAsync(new EmbedBuilder()
                                .WithAuthor(eab => eab.WithName($"{i} is currently away")
                                    .WithIconUrl(i.GetAvatarUrl()))
                                .WithDescription(afkmsg)
                                .WithOkColor());
                            return;
                        }
                    }
                }
            });
            return Task.CompletedTask;
        }

        public bool IsAfk(IGuild guild, IGuildUser user)
        {
            var afkmsg = AfkMessage(guild.Id, user.Id).Select(x => x.Message).Last();
            if (afkmsg == "") return false;
            return true;
        }

        public Task MessageUpdated(Cacheable<IMessage, ulong> msg, SocketMessage msg2, ISocketMessageChannel t)
        {
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

        public int GetAfkType(ulong? id)
        {
            _AfkType.TryGetValue(id.Value, out var snum);
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

        public async Task AFKSet(IGuild guild, IGuildUser user, string message)
        {
            var aFK = new AFK
            {
                GuildId = guild.Id,
                UserId = user.Id,
                Message = message
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