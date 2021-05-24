using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;

namespace Mewdeko.Modules.Utility.Services
{
    public class SuggestService : INService
    {
        private readonly DbService _db;


        public SuggestService(DbService db, Mewdeko bot)
        {
            _db = db;
            _snum = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.sugnum)
                .ToConcurrent();
            _sugchans = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.sugchan)
                .ToConcurrent();
            _sugroles = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.SuggestRole)
                .ToConcurrent();
        }

        private ConcurrentDictionary<ulong, ulong> _sugchans { get; } = new();
        private ConcurrentDictionary<ulong, ulong> _sugroles { get; } = new();
        private ConcurrentDictionary<ulong, ulong> _snum { get; } = new();

        public ulong GetSNum(ulong? id)
        {
            _snum.TryGetValue(id.Value, out var snum);
            return snum;
        }

        public async Task SetSuggestionChannelId(IGuild guild, ulong channel)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.sugchan = channel;
                await uow.SaveChangesAsync();
            }

            _sugchans.AddOrUpdate(guild.Id, channel, (key, old) => channel);
        }

        public async Task SetSuggestionRole(IGuild guild, ulong name)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.SuggestRole = name;
                await uow.SaveChangesAsync();
            }

            _sugroles.AddOrUpdate(guild.Id, name, (key, old) => name);
        }

        public async Task sugnum(IGuild guild, ulong num)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.sugnum = num;
                await uow.SaveChangesAsync();
            }

            _snum.AddOrUpdate(guild.Id, num, (key, old) => num);
        }

        public ulong GetSuggestionChannel(ulong? id)
        {
            if (id == null || !_sugchans.TryGetValue(id.Value, out var SugChan))
                return 0;

            return SugChan;
        }

        public ulong GetSuggestionRole(ulong? id)
        {
            if (id == null || !_sugroles.TryGetValue(id.Value, out var SugRole))
                return 0;

            return SugRole;
        }

        public async Task Suggest(IGuild guild, ulong SuggestID, ulong MessageID, ulong UserID)
        {
            var guildId = guild.Id;

            var suggest = new Suggestions
            {
                GuildId = guildId,
                SuggestID = SuggestID,
                MessageID = MessageID,
                UserID = UserID
            };
            using var uow = _db.GetDbContext();
            uow.Suggestions.Add(suggest);

            await uow.SaveChangesAsync();
        }

        public Suggestions[] Suggestions(ulong gid, ulong sid)
        {
            using var uow = _db.GetDbContext();
            return uow.Suggestions.ForId(gid, sid);
        }
    }
}