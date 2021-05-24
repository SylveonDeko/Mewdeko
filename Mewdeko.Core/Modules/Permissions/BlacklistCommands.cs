using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class BlacklistCommands : MewdekoSubmodule<BlacklistService>
        {
            private readonly IBotCredentials _creds;
            private readonly DbService _db;

            public BlacklistCommands(DbService db, IBotCredentials creds)
            {
                _db = db;
                _creds = creds;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public Task UserBlacklist(AddRemove action, ulong id)
            {
                return Blacklist(action, id, BlacklistType.User);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public Task UserBlacklist(AddRemove action, IUser usr)
            {
                return Blacklist(action, usr.Id, BlacklistType.User);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public Task ChannelBlacklist(AddRemove action, ulong id)
            {
                return Blacklist(action, id, BlacklistType.Channel);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public Task ServerBlacklist(AddRemove action, ulong id)
            {
                return Blacklist(action, id, BlacklistType.Server);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public Task ServerBlacklist(AddRemove action, IGuild guild)
            {
                return Blacklist(action, guild.Id, BlacklistType.Server);
            }

            private async Task Blacklist(AddRemove action, ulong id, BlacklistType type)
            {
                if (action == AddRemove.Add && _creds.OwnerIds.Contains(id))
                    return;

                using (var uow = _db.GetDbContext())
                {
                    if (action == AddRemove.Add)
                    {
                        var item = new BlacklistItem {ItemId = id, Type = type};
                        uow.BotConfig.GetOrCreate().Blacklist.Add(item);
                    }
                    else
                    {
                        var objs = uow.BotConfig
                            .GetOrCreate(set => set.Include(x => x.Blacklist))
                            .Blacklist
                            .Where(bi => bi.ItemId == id && bi.Type == type);

                        if (objs.Any())
                            uow._context.Set<BlacklistItem>().RemoveRange(objs);
                    }

                    await uow.SaveChangesAsync();
                }

                if (action == AddRemove.Add)
                    await ReplyConfirmLocalizedAsync("blacklisted", Format.Code(type.ToString()),
                        Format.Code(id.ToString())).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("unblacklisted", Format.Code(type.ToString()),
                        Format.Code(id.ToString())).ConfigureAwait(false);
            }
        }
    }
}