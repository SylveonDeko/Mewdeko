namespace Mewdeko.Modules.Permissions.Common;

/// <summary>
/// Provides extension methods for handling permissions within the Mewdeko permissions system.
/// These methods extend the functionality of Permissionv2 objects and collections thereof,
/// allowing for checks against messages, users, and specific command or module permissions.
/// </summary>
public static class PermissionExtensions
{
    /// <summary>
    /// Checks if the permissions allow for the execution of a specified command within a message context.
    /// </summary>
    /// <param name="permsEnumerable">An enumerable collection of Permissionv2 objects.</param>
    /// <param name="message">The message that potentially triggers the command.</param>
    /// <param name="commandName">The name of the command to check permissions against.</param>
    /// <param name="moduleName">The name of the module containing the command.</param>
    /// <param name="permIndex">Outputs the index of the permission that allowed or denied the command, if applicable.</param>
    /// <returns>True if the command is allowed by the permissions; otherwise, false.</returns>
    /// <remarks>
    /// This method evaluates the permissions in reverse order to prioritize more specific permissions over general ones.
    /// </remarks>
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

    /// <summary>
    /// Checks if slash command permissions allow for the execution of a specified command.
    /// </summary>
    /// <param name="permsEnumerable">An enumerable collection of Permissionv2 objects.</param>
    /// <param name="moduleName">The name of the module containing the slash command.</param>
    /// <param name="commandName">The name of the slash command to check permissions against.</param>
    /// <param name="user">The user attempting to execute the slash command.</param>
    /// <param name="chan">The channel in which the slash command was invoked.</param>
    /// <param name="permIndex">Outputs the index of the permission that allowed or denied the command, if applicable.</param>
    /// <returns>True if the slash command is allowed by the permissions; otherwise, false.</returns>
    /// <remarks>
    /// Similar to CheckPermissions, but tailored for slash commands and their distinct context.
    /// </remarks>
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

    /// <summary>
    /// Checks if a specific message has permission based on the detailed permission settings.
    /// </summary>
    /// <param name="perm">The permission to check against the message.</param>
    /// <param name="message">The message that potentially triggers the command.</param>
    /// <param name="commandName">The command name to check permissions against.</param>
    /// <param name="moduleName">The module name to check permissions against.</param>
    /// <returns>True if the permission is applicable and allowed, false if not allowed, or null if not applicable.</returns>
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

    /// <summary>
    /// Checks if a user has permission to execute a slash command based on specific permission settings.
    /// </summary>
    /// <param name="perm">The permission to check against the slash command.</param>
    /// <param name="moduleName">The module name containing the slash command.</param>
    /// <param name="user">The user attempting to execute the slash command.</param>
    /// <param name="commandName">The name of the slash command to check permissions against.</param>
    /// <param name="chan">The channel in which the slash command was invoked.</param>
    /// <returns>True if the permission is applicable and allowed, false if not allowed, or null if not applicable.</returns>
    public static bool? CheckSlashPermission(this Permissionv2 perm, string moduleName, IUser user, string commandName,
        IMessageChannel chan)
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

    /// <summary>
    /// Constructs a command string based on the permission settings.
    /// </summary>
    /// <param name="perm">The permission for which to construct the command string.</param>
    /// <param name="prefix">The command prefix used by the bot.</param>
    /// <param name="guild">Optional. The guild within which the command is relevant. This parameter can affect how user or role identifiers are resolved.</param>
    /// <returns>A string representing the constructed command based on the permission settings.</returns>
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

        var secName = perm.SecondaryTarget == SecondaryPermissionType.Command &&
                      perm.IsCustomCommand
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

    /// <summary>
    /// Enumerates through a linked list of permissions starting from the specified permission.
    /// </summary>
    /// <param name="perm">The starting permission from which to begin enumeration.</param>
    /// <returns>An enumerable sequence of permissions, including the starting permission and all subsequent linked permissions.</returns>
    public static IEnumerable<Permission> AsEnumerable(this Permission perm)
    {
        do
        {
            yield return perm;
        } while ((perm = perm.Next) != null);
    }
}