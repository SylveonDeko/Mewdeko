using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Core.Services.Database.Models;


namespace Mewdeko.Modules.Utility.Services
{
    public class AFKService : INService
    {
        private readonly DbService _db;
        public DiscordSocketClient _client;
        public AFKService(DbService db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
            _client.MessageReceived += MessageReceived;
            _client.MessageUpdated += MessageUpdated;
        }
        public Task MessageReceived(SocketMessage msg)
        {
            _ = Task.Run(async () =>
            {
                if (msg.MentionedUsers.Count > 0 && !msg.Author.IsBot)
                {

                    var IDs = msg.MentionedUsers;
                    
                        foreach (var i in IDs)
                        {
                            var chnl = (SocketGuildUser)msg.Author;
                            var Guild = chnl.Guild;
                            var afkmsg = AfkMessage(((IGuildChannel)msg.Channel).Guild.Id, i.Id).Select(x => x.Message).Last();
                            if (afkmsg == ""){return;}
                            await ((ITextChannel)msg.Channel).EmbedAsync(new EmbedBuilder()
                            .WithAuthor(eab => eab.WithName($"{i} is currently away")
                            .WithIconUrl(i.GetAvatarUrl()))
                            .WithDescription(afkmsg)
                            .WithOkColor());
                            return;
                        }
                    
                }
            });
            return Task.CompletedTask;
        }
        public Task MessageUpdated(Cacheable<IMessage, ulong> msg, SocketMessage msg2, ISocketMessageChannel t)
                => MessageReceived(msg2);

        public async Task AFKSet(IGuild guild, IGuildUser user, string message)
        {
            AFK aFK = new AFK()
            {
                GuildId = guild.Id,
                UserId = user.Id,
                Message = message
            };
            var afk = aFK;
            using var uow = _db.GetDbContext();
            uow.AFK.Add(afk);
            await uow.SaveChangesAsync();
        }
        public AFK[] AfkMessage(ulong gid, ulong uid)
        {
            using var uow = _db.GetDbContext();
            return uow.AFK.ForId(gid, uid);
        }
    }
}
