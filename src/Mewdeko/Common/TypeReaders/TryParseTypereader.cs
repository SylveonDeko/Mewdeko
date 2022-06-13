using Discord.Commands;
using System.Threading.Tasks;

namespace Mewdeko.Common.TypeReaders;

public class TryParseTypeReader<T> : TypeReader
{
    private readonly TryParseDelegate<T> _tryParse;

    public TryParseTypeReader(TryParseDelegate<T> tryParse) => _tryParse = tryParse;

    public override Task<TypeReaderResult> ReadAsync(
        ICommandContext context, string input, IServiceProvider services) =>
        _tryParse(input, out var result)
            ? Task.FromResult(TypeReaderResult.FromSuccess(result))
            : Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Invalid input"));
}

public class EnumTryParseTypeReader<T> : TypeReader where T : struct, Enum
{
    private readonly bool _ignoreCase;

    public EnumTryParseTypeReader(bool ignoreCase = true) => _ignoreCase = ignoreCase;

    public override Task<TypeReaderResult> ReadAsync(
        ICommandContext context, string input, IServiceProvider services) =>
        Enum.TryParse<T>(input, _ignoreCase, out var result) && Enum.IsDefined(result)
            ? Task.FromResult(TypeReaderResult.FromSuccess(result))
            : Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, $"Failed to parse {input}."));
}