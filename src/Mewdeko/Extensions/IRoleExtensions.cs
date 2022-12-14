namespace Mewdeko.Extensions;

public static class RoleExtensions
{
    public static bool CanManageRole(this IRole role, IGuildUser user)
    {
        if (user.Guild.OwnerId == user.Id)
            return !role.IsManaged && role.Id != user.Guild.EveryoneRole.Id;
        if (!user.GuildPermissions.Has(GuildPermission.ManageRoles) && !user.GuildPermissions.Has(GuildPermission.Administrator))
            return false;
        return role.Position < user.GetRoles().Max(x => x.Position) && !role.IsManaged && role.Id != user.Guild.EveryoneRole.Id;
    }
}