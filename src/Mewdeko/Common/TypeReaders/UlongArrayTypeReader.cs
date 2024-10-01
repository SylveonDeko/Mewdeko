using Discord.Commands;

namespace Mewdeko.Common.TypeReaders;

/// <summary>
///     Type reader for parsing input strings into arrays of ulong values.
/// </summary>
public class UlongArrayTypeReader : MewdekoTypeReader<ulong[]>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="UlongArrayTypeReader" /> class.
    /// </summary>
    /// <param name="client">The DiscordShardedClient instance.</param>
    /// <param name="cmds">The CommandService instance.</param>
    public UlongArrayTypeReader(DiscordShardedClient client, CommandService cmds) : base(client, cmds)
    {
    }

    /// <inheritdoc />
    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
    {
        // Split input string into an array of substrings based on delimiters
        var inputs = input.Split(' ', ',', '.', ':', '-');

        // Filter out empty substrings and convert each valid substring to ulong, then store in a list
        var data = inputs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Convert.ToUInt64(x)).ToList();

        // Return a TypeReaderResult indicating success with the parsed ulong array,
        // or an error if parsing fails
        return Task.FromResult(data != null
            ? TypeReaderResult.FromSuccess(data)
            : TypeReaderResult.FromError(CommandError.ParseFailed,
                "Failed to parse input as an array of ulong values."));
    }
}