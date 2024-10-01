using Discord.Commands;
using Mewdeko.Common.TypeReaders.Models;

namespace Mewdeko.Common.TypeReaders;

/// <summary>
///     Type reader for parsing permission action inputs into PermissionAction objects.
///     Used instead of bool for more flexible keywords for true/false only in the permission module.
/// </summary>
public class PermissionActionTypeReader : MewdekoTypeReader<PermissionAction>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PermissionActionTypeReader" /> class.
    /// </summary>
    /// <param name="client">The DiscordShardedClient instance.</param>
    /// <param name="cmds">The CommandService instance.</param>
    public PermissionActionTypeReader(DiscordShardedClient client, CommandService cmds) : base(client, cmds)
    {
    }

    /// <inheritdoc />
    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
        IServiceProvider _)
    {
        await Task.CompletedTask; // Async method requires an awaitable expression, Task.CompletedTask is used as a placeholder
        input = input.ToUpperInvariant(); // Converts input string to uppercase for case-insensitive comparison

        // Switch statement to map input strings to PermissionAction values
        return input switch
        {
            // Enable values
            "1" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "T" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "TRUE" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "ENABLE" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "ENABLED" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "ALLOW" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "PERMIT" => TypeReaderResult.FromSuccess(PermissionAction.Enable),
            "UNBAN" => TypeReaderResult.FromSuccess(PermissionAction.Enable),

            // Disable values
            "0" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "F" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "FALSE" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "DENY" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "DISABLE" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "DISABLED" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "DISALLOW" => TypeReaderResult.FromSuccess(PermissionAction.Disable),
            "BAN" => TypeReaderResult.FromSuccess(PermissionAction.Disable),

            // Error case
            _ => TypeReaderResult.FromError(CommandError.ParseFailed,
                "Must be either deny/allow or enabled/disabled.")
        };
    }
}