using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.TypeReaders.Models;

namespace Mewdeko.Common.TypeReaders;

/// <summary>
///     Used instead of bool for more flexible keywords for true/false only in the permission module
/// </summary>
public class PermissionActionTypeReader : MewdekoTypeReader<PermissionAction>
{
    public PermissionActionTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
    {
    }

    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
    {
        await Task.CompletedTask;
        input = input.ToUpperInvariant();
        return input switch
        {
            "1" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "T" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "TRUE" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "ENABLE" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "ENABLED" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "ALLOW" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "PERMIT" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "UNBAN" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "0" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "F" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "FALSE" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "DENY" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "DISABLE" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "DISABLED" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "DISALLOW" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "BAN" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            _ => TypeReaderResult.FromError(CommandError.ParseFailed, "Must be either deny/allow or enabled/disabled.")
        };
    }
}