using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// Service for automatically banning users who add a specified AutoBanRole.
/// </summary>
public class AutoBanRoleService : INService
{
    private readonly DbService dbService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoBanRoleService"/> class.
    /// </summary>
    /// <param name="handler">The event handler</param>
    /// <param name="db">The database handler</param>
    public AutoBanRoleService(EventHandler handler, DbService db)
    {
        dbService = db;
        handler.GuildMemberUpdated += OnGuildMemberUpdated;
    }


    private async Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> args, SocketGuildUser arsg2)
    {
        var addedRoles = arsg2.Roles.Except(args.Value.Roles);
        if (!addedRoles.Any()) return;

        await using var uow = dbService.GetDbContext();
        var autoBanRoles = await uow.AutoBanRoles.AsQueryable().Where(x => x.GuildId == arsg2.Guild.Id).ToListAsync();
        var roles = autoBanRoles.Select(x => x.RoleId).ToHashSet();
        if (!addedRoles.Any(x => roles.Contains(x.Id))) return;

        await arsg2.Guild.AddBanAsync(arsg2, 0, "Auto-ban role");
    }

    /// <summary>
    /// Adds a role to the list of AutoBanRoles.
    /// </summary>
    /// <param name="guildId">The guild id</param>
    /// <param name="roleId">The role to add to autoban</param>
    /// <returns>A bool depending on whether the role was removed</returns>
    public async Task<bool> AddAutoBanRole(ulong guildId, ulong roleId)
    {
        await using var uow = dbService.GetDbContext();
        var autoBanRole = await uow.AutoBanRoles.AsQueryable()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == roleId);
        if (autoBanRole != null) return false;

        await uow.AutoBanRoles.AddAsync(new AutoBanRoles
        {
            GuildId = guildId, RoleId = roleId
        });
        await uow.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Removes a role from the list of AutoBanRoles.
    /// </summary>
    /// <param name="guildId">The guild id</param>
    /// <param name="roleId">The role to remove</param>
    /// <returns>A bool depending on whether the role was removed</returns>
    public async Task<bool> RemoveAutoBanRole(ulong guildId, ulong roleId)
    {
        await using var uow = dbService.GetDbContext();
        var autoBanRole = await uow.AutoBanRoles.AsQueryable()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == roleId);
        if (autoBanRole == null) return false;

        uow.AutoBanRoles.Remove(autoBanRole);
        await uow.SaveChangesAsync();
        return true;
    }
}