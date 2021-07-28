using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Core.Common;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Utility.Services
{
    public class ReputationService : INService
    {
        private readonly DbService _db;
        private readonly IPubSub _pubSub;

        private readonly TypedKey<RepBlacklistEntry[]> blPubKey = new("repblacklist.reload");
        public IReadOnlyList<RepBlacklistEntry> _blacklist;

        public ReputationService(DbService db, IPubSub pubSub)
        {
            _db = db;
            _pubSub = pubSub;

            Reload(false);
            _pubSub.Sub(blPubKey, OnReload);
        }

        private ValueTask OnReload(RepBlacklistEntry[] blacklist)
        {
            _blacklist = blacklist;
            return default;
        }

        public async Task AddRep(IGuild guild, IGuildUser user, IGuildUser reviewer, string message, int reptype)
        {
            var aFK = new Reputation
            {
                GuildId = guild.Id,
                UserId = user.Id,
                ReviewerId = reviewer.Id,
                ReviewMessage = message,
                ReviewType = reptype,
                ReviewerUsername = reviewer.ToString(),
                ReviewerAv = reviewer.RealAvatarUrl(2048).ToString()
            };
            var afk = aFK;
            using var uow = _db.GetDbContext();
            uow.Reputation.Add(afk);
            await uow.SaveChangesAsync();
        }

        public void Reload(bool publish = true)
        {
            using var uow = _db.GetDbContext();
            var toPublish = uow._context.RepBlacklist.AsNoTracking().ToArray();
            _blacklist = toPublish;
            if (publish) _pubSub.Pub(blPubKey, toPublish);
        }

        public void Blacklist(ulong id)
        {
            using var uow = _db.GetDbContext();
            var item = new RepBlacklistEntry {ItemId = id};
            uow._context.RepBlacklist.Add(item);
            uow.SaveChanges();

            Reload();
        }

        public void UnBlacklist(ulong id)
        {
            using var uow = _db.GetDbContext();
            var toRemove = uow._context.RepBlacklist
                .FirstOrDefault(bi => bi.ItemId == id);

            if (!(toRemove is null))
                uow._context.RepBlacklist.Remove(toRemove);

            uow.SaveChanges();

            Reload();
        }

        public Reputation[] Reputations(ulong uid)
        {
            using var uow = _db.GetDbContext();
            return uow.Reputation.ForUserId(uid);
        }

        public Reputation[] ServerReputations(ulong gid)
        {
            using var uow = _db.GetDbContext();
            return uow.Reputation.ForGuildId(gid);
        }

        public Reputation[] ReviewerReps(ulong uid)
        {
            using var uow = _db.GetDbContext();
            return uow.Reputation.ForReviewerId(uid);
        }
    }
}