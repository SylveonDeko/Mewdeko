using System.Threading.Tasks;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Modules.Permissions.Services
{
    public class ResetPermissionsService : INService
    {
        private readonly DbService _db;
        private readonly GlobalPermissionService _globalPerms;
        private readonly PermissionService _perms;

        public ResetPermissionsService(PermissionService perms, GlobalPermissionService globalPerms, DbService db)
        {
            _perms = perms;
            _globalPerms = globalPerms;
            _db = db;
        }

        public async Task ResetPermissions(ulong guildId)
        {
            using (var uow = _db.GetDbContext())
            {
                var config = uow.GuildConfigs.GcWithPermissionsv2For(guildId);
                config.Permissions = Permissionv2.GetDefaultPermlist;
                await uow.SaveChangesAsync();
                _perms.UpdateCache(config);
            }
        }

        public async Task ResetGlobalPermissions()
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.BotConfig.GetOrCreate();
                gc.BlockedCommands.Clear();
                gc.BlockedModules.Clear();

                _globalPerms.BlockedCommands.Clear();
                _globalPerms.BlockedModules.Clear();
                await uow.SaveChangesAsync();
            }
        }
    }
}