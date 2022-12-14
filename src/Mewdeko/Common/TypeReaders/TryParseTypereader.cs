using System.Threading.Tasks;
using Discord.Commands;

namespace Mewdeko.Common.TypeReaders;

public class TryParseTypeReader<T> : TypeReader
{
    private readonly TryParseDelegate<T> tryParse;

    public TryParseTypeReader(TryParseDelegate<T> tryParse) => this.tryParse = tryParse;

    public override Task<TypeReaderResult> ReadAsync(
        ICommandContext context, string input, IServiceProvider services) =>
        tryParse(input, out var result)
            ? Task.FromResult(TypeReaderResult.FromSuccess(result))
            : Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Invalid input"));
}

public class EnumTryParseTypeReader<T> : TypeReader where T : struct, Enum
{
    private readonly bool ignoreCase;

    public EnumTryParseTypeReader(bool ignoreCase = true) => this.ignoreCase = ignoreCase;

    public override Task<TypeReaderResult> ReadAsync(
        ICommandContext context, string input, IServiceProvider services) =>
        Enum.TryParse<T>(input, ignoreCase, out var result) && Enum.IsDefined(result)
            ? Task.FromResult(TypeReaderResult.FromSuccess(result))
            : Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, $"Failed to parse {input}."));
}