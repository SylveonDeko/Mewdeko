using Discord.Commands;

namespace Mewdeko.Common.TypeReaders;

public class UlongArrayTypeReader : MewdekoTypeReader<ulong[]>
{

    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
    {
        var inputs = input.Split(' ', ',', '.', ':', '-');
        var data = inputs.Where(x => !x.IsNullOrWhiteSpace()).Select(x => Convert.ToUInt64(x)).ToList();

        return Task.FromResult(data != null ? TypeReaderResult.FromSuccess(data) : TypeReaderResult.FromError(CommandError.ParseFailed, "obonobo"));
    }

    public UlongArrayTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
    {
    }
}