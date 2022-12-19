using System.Threading.Tasks;
using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Permissions.Services;

public class GlobalPermissionService : ILateBlocker, INService
{
    private readonly BotConfigService bss;

    public GlobalPermissionService(BotConfigService bss) => this.bss = bss;

    public HashSet<string> BlockedCommands => bss.Data.Blocked.Commands;
    public HashSet<string> BlockedModules => bss.Data.Blocked.Modules;
    public int Priority { get; } = 0;

    public Task<bool> TryBlockLate(DiscordSocketClient client, ICommandContext ctx, string moduleName,
        CommandInfo command)
    {
        var settings = bss.Data;
        var commandName = command.Name.ToLowerInvariant();

        return Task.FromResult(commandName != "resetglobalperms" &&
                               (settings.Blocked.Commands.Contains(commandName) ||
                                settings.Blocked.Modules.Contains(moduleName.ToLowerInvariant())));
    }

    public Task<bool> TryBlockLate(DiscordSocketClient client, IInteractionContext ctx,
        ICommandInfo command)
    {
        var settings = bss.Data;
        var commandName = command.MethodName.ToLowerInvariant();

        return Task.FromResult(commandName != "resetglobalperms" &&
                               settings.Blocked.Commands.Contains(commandName));
    }

    /// <summary>
    ///     Toggles module blacklist
    /// </summary>
    /// <param name="moduleName">Lowercase module name</param>
    /// <returns>Whether the module is added</returns>
    public bool ToggleModule(string moduleName)
    {
        var added = false;
        bss.ModifyConfig(bs =>
        {
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
    ///     Toggles command blacklist
    /// </summary>
    /// <param name="commandName">Lowercase command name</param>
    /// <returns>Whether the command is added</returns>
    public bool ToggleCommand(string commandName)
    {
        var added = false;
        bss.ModifyConfig(bs =>
        {
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
    ///     Resets all global permissions
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