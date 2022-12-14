using System.Threading.Tasks;
using Discord.Commands;

namespace Mewdeko.Common.TypeReaders;

/// <summary>
///     Used instead of bool for more flexible keywords for true/false only in the permission module
/// </summary>
public class PermValue : MewdekoTypeReader<PermValue>
{
    public PermValue(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
    {
    }

    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
    {
        input = input.ToUpperInvariant();
        return input switch
        {
            "1" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Allow)),
            "T" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Allow)),
            "TRUE" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Allow)),
            "ENABLE" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Allow)),
            "ENABLED" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Allow)),
            "ALLOW" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Allow)),
            "PERMIT" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Allow)),
            "UNBAN" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Allow)),
            "0" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Deny)),
            "F" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Deny)),
            "FALSE" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Deny)),
            "DENY" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Deny)),
            "DISABLE" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Deny)),
            "DISABLED" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Deny)),
            "DISALLOW" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Deny)),
            "BAN" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Deny)),
            "2" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Inherit)),
            "N" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Inherit)),
            "Neutral" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Inherit)),
            "Inherit" => Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Inherit)),
            _ => Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Must be either deny or allow."))
        };
    }
}