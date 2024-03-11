using Discord.Commands;
using SkiaSharp;

namespace Mewdeko.Common.TypeReaders;

public class SkColorTypeReader : MewdekoTypeReader<SKColor>
{
    public SkColorTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
    {
    }

    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
        IServiceProvider services)
    {
        await Task.Yield();

        input = input.Replace("#", "", StringComparison.InvariantCulture);
        try
        {
            return TypeReaderResult.FromSuccess(SKColor.Parse(input));
        }
        catch
        {
            return TypeReaderResult.FromError(CommandError.ParseFailed,
                "Parameter is not a valid color hex or name.");
        }
    }
}