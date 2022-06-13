using Discord.Commands;
using Mewdeko.Common.TypeReaders.Models;
using System.Threading.Tasks;

namespace Mewdeko.Common.TypeReaders;

/// <summary>
///     Used instead of bool for more flexible keywords for true/false only in the permission module
/// </summary>
public class PermissionActionTypeReader : MewdekoTypeReader<PermissionAction>
{
    public PermissionActionTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
    {
    }

    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
    {
        input = input.ToUpperInvariant();
        return input switch
        {
            "1" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Enable)),
            "T" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Enable)),
            "TRUE" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Enable)),
            "ENABLE" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Enable)),
            "ENABLED" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Enable)),
            "ALLOW" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Enable)),
            "PERMIT" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Enable)),
            "UNBAN" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Enable)),
            "0" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Disable)),
            "F" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Disable)),
            "FALSE" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Disable)),
            "DENY" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Disable)),
            "DISABLE" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Disable)),
            "DISABLED" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Disable)),
            "DISALLOW" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Disable)),
            "BAN" => Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Disable)),
            _ => Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Must be either deny or allow."))
        };
    }
}