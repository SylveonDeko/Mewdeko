using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Extensions;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Modules.ServerManagement.Services
{
    public class ServerManagementService : INService
    {
        public ConcurrentDictionary<ulong, string> GuildMuteRoles { get; }
        public DiscordSocketClient _client;
        private readonly DbService _db;
        private static readonly OverwritePermissions denyOverwrite =
            new OverwritePermissions(addReactions: PermValue.Deny, sendMessages: PermValue.Deny,
                attachFiles: PermValue.Deny, viewChannel: PermValue.Deny);
                public CommandContext ctx;
        public ServerManagementService(DiscordSocketClient client, DbService db)
        {
            _client = client;
            _db = db;

            using (var uow = db.GetDbContext())
        {
            var guildIds = client.Guilds.Select(x => x.Id).ToList();
             var configs = uow._context.Set<GuildConfig>().AsQueryable()
                .Where(x => guildIds.Contains(x.GuildId))
                .ToList();

            GuildMuteRoles = configs
                .Where(c => !string.IsNullOrWhiteSpace(c.MuteRoleName))
                .ToDictionary(c => c.GuildId, c => c.MuteRoleName)
                .ToConcurrent();
        }
    }
        public async Task<IRole> GetMuteRole(IGuild guild)
        {
            if (guild == null)
                throw new ArgumentNullException(nameof(guild));

            const string defaultMuteRoleName = "nadeko-mute";

            var muteRoleName = GuildMuteRoles.GetOrAdd(guild.Id, defaultMuteRoleName);

            var muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName);
            if (muteRole == null)
            {

                //if it doesn't exist, create it
                try { muteRole = await guild.CreateRoleAsync(muteRoleName, isMentionable: false).ConfigureAwait(false); }
                catch
                {
                    //if creations fails,  maybe the name != correct, find default one, if doesn't work, create default one
                    muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName) ??
                        await guild.CreateRoleAsync(defaultMuteRoleName, isMentionable: false).ConfigureAwait(false);
                }
            }
             return muteRole;
        }
    }
}