using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.TypeReaders.Models;

namespace Mewdeko.Common.TypeReaders;

public class StoopidTimeTypeReader : MewdekoTypeReader<StoopidTime>
{
    public StoopidTimeTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
    {
    }

    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
        IServiceProvider services)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(TypeReaderResult.FromError(CommandError.Unsuccessful, "Input is empty."));
        try
        {
            var time = StoopidTime.FromInput(input);
            return Task.FromResult(TypeReaderResult.FromSuccess(time));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TypeReaderResult.FromError(CommandError.Exception, ex.Message));
        }
    }
}