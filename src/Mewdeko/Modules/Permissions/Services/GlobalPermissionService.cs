using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Permissions.Services;

/// <summary>
///     Provides a service for managing global permissions, allowing for the blocking of specific commands and modules.
/// </summary>
public class GlobalPermissionService : ILateBlocker, INService
{
    private readonly BotConfigService bss;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GlobalPermissionService" /> class.
    /// </summary>
    /// <param name="bss">The bot configuration service.</param>
    public GlobalPermissionService(BotConfigService bss)
    {
        this.bss = bss;
    }

    /// <summary>
    ///     Gets a collection of blocked command names.
    /// </summary>
    public HashSet<string> BlockedCommands
    {
        get
        {
            return bss.Data.Blocked.Commands;
        }
    }

    /// <summary>
    ///     Gets a collection of blocked module names.
    /// </summary>
    public HashSet<string> BlockedModules
    {
        get
        {
            return bss.Data.Blocked.Modules;
        }
    }

    /// <summary>
    ///     Gets the priority of the service in the execution pipeline.
    /// </summary>
    public int Priority { get; } = 0;

    /// <summary>
    ///     Attempts to block the execution of a command based on global permissions settings.
    /// </summary>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="ctx">The command context.</param>
    /// <param name="moduleName">The name of the module containing the command.</param>
    /// <param name="command">The command being executed.</param>
    /// <returns>A task that resolves to true if the command should be blocked; otherwise, false.</returns>
    public Task<bool> TryBlockLate(DiscordShardedClient client, ICommandContext ctx, string moduleName,
        CommandInfo command)
    {
        var settings = bss.Data;
        var commandName = command.Name.ToLowerInvariant();

        // If the command or module is blocked, prevent its execution unless it's the resetglobalperms command
        return Task.FromResult(commandName != "resetglobalperms" &&
                               (settings.Blocked.Commands.Contains(commandName) ||
                                settings.Blocked.Modules.Contains(moduleName.ToLowerInvariant())));
    }

    /// <summary>
    ///     Attempts to block the execution of a slash command based on global permissions settings.
    /// </summary>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="command">The slash command being executed.</param>
    /// <returns>A task that resolves to true if the command should be blocked; otherwise, false.</returns>
    public Task<bool> TryBlockLate(DiscordShardedClient client, IInteractionContext ctx,
        ICommandInfo command)
    {
        var settings = bss.Data;
        var commandName = command.MethodName.ToLowerInvariant();

        // If the command is blocked, prevent its execution unless it's the resetglobalperms command
        return Task.FromResult(commandName != "resetglobalperms" &&
                               settings.Blocked.Commands.Contains(commandName));
    }

    /// <summary>
    ///     Toggles the blocking status of a module. If the module is currently blocked, it will be unblocked, and vice versa.
    /// </summary>
    /// <param name="moduleName">The name of the module to toggle, in lowercase.</param>
    /// <returns>True if the module was added to the blocked list, false if it was removed.</returns>
    public bool ToggleModule(string moduleName)
    {
        var added = false;
        bss.ModifyConfig(bs =>
        {
            // Add the module to the block list if it's not already there; otherwise, remove it
            if (bs.Blocked.Modules.Add(moduleName))
            {
                added = true;
            }
            else
            {
                bs.Blocked.Modules.Remove(moduleName);
                added = false;
            }
        });

        return added;
    }

    /// <summary>
    ///     Toggles the blocking status of a command. If the command is currently blocked, it will be unblocked, and vice
    ///     versa.
    /// </summary>
    /// <param name="commandName">The name of the command to toggle, in lowercase.</param>
    /// <returns>True if the command was added to the blocked list, false if it was removed.</returns>
    public bool ToggleCommand(string commandName)
    {
        var added = false;
        bss.ModifyConfig(bs =>
        {
            // Add the command to the block list if it's not already there; otherwise, remove it
            if (bs.Blocked.Commands.Add(commandName))
            {
                added = true;
            }
            else
            {
                bs.Blocked.Commands.Remove(commandName);
                added = false;
            }
        });

        return added;
    }

    /// <summary>
    ///     Resets all global permissions, clearing both the command and module block lists.
    /// </summary>
    public Task Reset()
    {
        bss.ModifyConfig(bs =>
        {
            bs.Blocked.Commands.Clear();
            bs.Blocked.Modules.Clear();
        });

        return Task.CompletedTask;
    }
}