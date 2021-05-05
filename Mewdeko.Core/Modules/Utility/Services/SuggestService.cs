using System.Threading.Tasks;
using Discord;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;


namespace Mewdeko.Modules.Utility.Services
{
    public class SuggestService : INService
    {
        private readonly DbService _db;

        public SuggestService(DbService db)
        {
            _db = db;
        }
        public async Task Suggest(IGuild guild, ulong SuggestID, ulong MessageID, ulong UserID)
        {

            var guildId = guild.Id;

            var suggest = new Suggestions()
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