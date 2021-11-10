using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Modules.Server_Management.Services
{
    public class ServerManagementService : INService
    {
        private static readonly OverwritePermissions denyOverwrite =
            new(addReactions: PermValue.Deny, sendMessages: PermValue.Deny,
                attachFiles: PermValue.Deny, viewChannel: PermValue.Deny);

        private readonly Mewdeko.Services.Mewdeko _bot;
        private readonly DbService _db;
        public DiscordSocketClient _client;

        public CommandContext ctx;

        public ServerManagementService(DiscordSocketClient client, DbService db, Mewdeko.Services.Mewdeko bot)
        {
            _client = client;
            _db = db;
            _bot = bot;
            _ticketchannelids = bot.AllGuildConfigs
                .Where(x => x.TicketCategory != 0)
                .ToDictionary(x => x.GuildId, x => x.TicketCategory)
                .ToConcurrent();

            using var uow = db.GetDbContext();
            var guildIds = client.Guilds.Select(x => x.Id).ToList();
            var configs = uow._context.Set<GuildConfig>().AsQueryable()
                .Where(x => guildIds.Contains(x.GuildId))
                .ToList();

            GuildMuteRoles = configs
                .Where(c => !string.IsNullOrWhiteSpace(c.MuteRoleName))
                .ToDictionary(c => c.GuildId, c => c.MuteRoleName)
                .ToConcurrent();
        }

        public ConcurrentDictionary<ulong, string> GuildMuteRoles { get; }
        private ConcurrentDictionary<ulong, ulong> _ticketchannelids { get; } = new();

        public ulong GetTicketCategory(ulong? id)
        {
            if (id == null || !_ticketchannelids.TryGetValue(id.Value, out var ticketcat))
                return 0;

            return ticketcat;
        }

        public async Task SetTicketCategoryId(IGuild guild, ICategoryChannel channel)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.TicketCategory = channel.Id;
                await uow.SaveChangesAsync();
            }

            _ticketchannelids.AddOrUpdate(guild.Id, channel.Id, (key, old) => channel.Id);
        }

        public async Task<IRole> GetMuteRole(IGuild guild)
        {
            if (guild == null)
                throw new ArgumentNullException(nameof(guild));

            const string defaultMuteRoleName = "Mewdeko-mute";

            var muteRoleName = GuildMuteRoles.GetOrAdd(guild.Id, defaultMuteRoleName);

            var muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName);
            if (muteRole == null)
                //if it doesn't exist, create it
                try
                {
                    muteRole = await guild.CreateRoleAsync(muteRoleName, isMentionable: false).ConfigureAwait(false);
                }
                catch
                {
                    //if creations fails,  maybe the name != correct, find default one, if doesn't work, create default one
                    muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName) ??
                               await guild.CreateRoleAsync(defaultMuteRoleName, isMentionable: false)
                                   .ConfigureAwait(false);
                }

            return muteRole;
        }
    }
}