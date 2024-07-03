using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// Service for automatically banning users who add a specified AutoBanRole.
/// </summary>
public class AutoBanRoleService : INService
{
    private readonly DbContextProvider dbProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoBanRoleService"/> class.
    /// </summary>
    /// <param name="handler">The event handler</param>
    /// <param name="db">The database handler</param>
    public AutoBanRoleService(EventHandler handler, DbContextProvider dbProvider)
    {
        this.dbProvider = dbProvider;
        handler.GuildMemberUpdated += OnGuildMemberUpdated;
    }


    private async Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> args, SocketGuildUser arsg2)
    {
        var addedRoles = arsg2.Roles.Except(args.Value.Roles);
        if (!addedRoles.Any()) return;

        await using var dbContext = await dbProvider.GetContextAsync();
        var autoBanRoles = await dbContext.AutoBanRoles.AsQueryable().Where(x => x.GuildId == arsg2.Guild.Id).ToListAsync();
        var roles = autoBanRoles.Select(x => x.RoleId).ToHashSet();
        if (!addedRoles.Any(x => roles.Contains(x.Id))) return;

        try
        {
            await arsg2.Guild.AddBanAsync(arsg2, 0, "Auto-ban role");
        }
        catch
        {
            //ignored
        }
    }

    /// <summary>
    /// Adds a role to the list of AutoBanRoles.
    /// </summary>
    /// <param name="guildId">The guild id</param>
    /// <param name="roleId">The role to add to autoban</param>
    /// <returns>A bool depending on whether the role was removed</returns>
    public async Task<bool> AddAutoBanRole(ulong guildId, ulong roleId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var autoBanRole = await dbContext.AutoBanRoles.AsQueryable()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == roleId);
        if (autoBanRole != null) return false;

        await dbContext.AutoBanRoles.AddAsync(new AutoBanRoles
        {
            GuildId = guildId, RoleId = roleId
        });
        await dbContext.SaveChangesAsync();
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
        await using var dbContext = await dbProvider.GetContextAsync();
        var autoBanRole = await dbContext.AutoBanRoles.AsQueryable()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == roleId);
        if (autoBanRole == null) return false;

        dbContext.AutoBanRoles.Remove(autoBanRole);
        await dbContext.SaveChangesAsync();
        return true;
    }
}