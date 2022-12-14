namespace Mewdeko.Modules.Permissions.Common;

public static class PermissionExtensions
{
    public static bool CheckPermissions(this IEnumerable<Permissionv2> permsEnumerable, IUserMessage message,
        string commandName, string moduleName, out int permIndex)
    {
        var perms = permsEnumerable as List<Permissionv2> ?? permsEnumerable.ToList();

        for (var i = perms.Count - 1; i >= 0; i--)
        {
            var perm = perms[i];

            var result = perm.CheckPermission(message, commandName, moduleName);

            if (result == null) continue;
            permIndex = i;
            return result.Value;
        }

        permIndex = -1; //defaut behaviour
        return true;
    }

    public static bool CheckSlashPermissions(this IEnumerable<Permissionv2> permsEnumerable,
        string moduleName, string commandName, IUser user, IMessageChannel chan, out int permIndex)
    {
        var perms = permsEnumerable as List<Permissionv2> ?? permsEnumerable.ToList();

        for (var i = perms.Count - 1; i >= 0; i--)
        {
            var perm = perms[i];

            var result = perm.CheckSlashPermission(moduleName, user, commandName, chan);

            if (result == null) continue;
            permIndex = i;
            return result.Value;
        }

        permIndex = -1; //defaut behaviour
        return true;
    }

    //null = not applicable
    //true = applicable, allowed
    //false = applicable, not allowed
    public static bool? CheckPermission(this Permissionv2 perm, IUserMessage message, string commandName,
        string moduleName)
    {
        if (!((perm.SecondaryTarget == SecondaryPermissionType.Command &&
               string.Equals(perm.SecondaryTargetName, commandName, StringComparison.InvariantCultureIgnoreCase)) ||
              (perm.SecondaryTarget == SecondaryPermissionType.Module &&
               string.Equals(perm.SecondaryTargetName, moduleName, StringComparison.InvariantCultureIgnoreCase)) ||
              perm.SecondaryTarget == SecondaryPermissionType.AllModules))
        {
            return null;
        }

        switch (perm.PrimaryTarget)
        {
            case PrimaryPermissionType.User:
                if (perm.PrimaryTargetId == message.Author.Id)
                    return perm.State;
                break;
            case PrimaryPermissionType.Channel:
                if (perm.PrimaryTargetId == message.Channel.Id)
                    return perm.State;
                break;
            case PrimaryPermissionType.Category:
                if (perm.PrimaryTargetId == ((ITextChannel)message.Channel).CategoryId)
                    return perm.State;
                break;
            case PrimaryPermissionType.Role:
                if (message.Author is not IGuildUser guildUser)
                    break;
                if (guildUser.RoleIds.Contains(perm.PrimaryTargetId))
                    return perm.State;
                break;
            case PrimaryPermissionType.Server:
                if (message.Author is not IGuildUser guildUser1)
                    break;
                if (guildUser1 == null)
                    break;
                return perm.State;
        }

        return null;
    }

    public static bool? CheckSlashPermission(this Permissionv2 perm, string moduleName, IUser user, string commandName, IMessageChannel chan)
    {
        if (!((perm.SecondaryTarget == SecondaryPermissionType.Command &&
               string.Equals(perm.SecondaryTargetName, commandName, StringComparison.InvariantCultureIgnoreCase)) ||
              (perm.SecondaryTarget == SecondaryPermissionType.Module &&
               string.Equals(perm.SecondaryTargetName, moduleName, StringComparison.InvariantCultureIgnoreCase)) ||
              perm.SecondaryTarget == SecondaryPermissionType.AllModules))
        {
            return null;
        }

        switch (perm.PrimaryTarget)
        {
            case PrimaryPermissionType.User:
                if (perm.PrimaryTargetId == user.Id)
                    return perm.State;
                break;
            case PrimaryPermissionType.Channel:
                if (perm.PrimaryTargetId == chan.Id)
                    return perm.State;
                break;
            case PrimaryPermissionType.Category:
                if (perm.PrimaryTargetId == ((ITextChannel)chan).CategoryId)
                    return perm.State;
                break;
            case PrimaryPermissionType.Role:
                if (user is not IGuildUser guildUser)
                    break;
                if (guildUser.RoleIds.Contains(perm.PrimaryTargetId))
                    return perm.State;
                break;
            case PrimaryPermissionType.Server:
                if (user is not IGuildUser guildUser1)
                    break;
                if (guildUser1 == null)
                    break;
                return perm.State;
        }

        return null;
    }

    public static string GetCommand(this Permissionv2 perm, string? prefix, SocketGuild? guild = null)
    {
        var com = "";
        switch (perm.PrimaryTarget)
        {
            case PrimaryPermissionType.User:
                com += "u";
                break;
            case PrimaryPermissionType.Channel:
                com += "c";
                break;
            case PrimaryPermissionType.Category:
                com += "ca";
                break;
            case PrimaryPermissionType.Role:
                com += "r";
                break;
            case PrimaryPermissionType.Server:
                com += "s";
                break;
        }

        switch (perm.SecondaryTarget)
        {
            case SecondaryPermissionType.Module:
                com += "m";
                break;
            case SecondaryPermissionType.Command:
                com += "c";
                break;
            case SecondaryPermissionType.AllModules:
                com = $"a{com}m";
                break;
        }

        var secName = perm.SecondaryTarget == SecondaryPermissionType.Command && !perm.IsCustomCommand
            ? prefix + perm.SecondaryTargetName
            : perm.SecondaryTargetName;
        com += $" {(perm.SecondaryTargetName != "*" ? $"{secName} " : "")}{(perm.State ? "enable" : "disable")} ";

        switch (perm.PrimaryTarget)
        {
            case PrimaryPermissionType.User:
                com += guild?.GetUser(perm.PrimaryTargetId)?.ToString() ?? $"<@{perm.PrimaryTargetId}>";
                break;
            case PrimaryPermissionType.Channel:
                com += $"<#{perm.PrimaryTargetId}>";
                break;
            case PrimaryPermissionType.Category:
                com += $"<#{perm.PrimaryTargetId}>";
                break;
            case PrimaryPermissionType.Role:
                com += guild?.GetRole(perm.PrimaryTargetId)?.ToString() ?? $"<@&{perm.PrimaryTargetId}>";
                break;
            case PrimaryPermissionType.Server:
                break;
        }

        return prefix + com;
    }

    public static IEnumerable<Permission> AsEnumerable(this Permission perm)
    {
        do
        {
            yield return perm;
        } while ((perm = perm.Next) != null);
    }
}