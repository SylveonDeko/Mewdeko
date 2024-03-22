using Discord.Commands;

namespace Mewdeko.Common.TypeReaders
{
    /// <summary>
    /// Type reader for parsing input strings into objects of type T using a custom TryParse method.
    /// </summary>
    /// <typeparam name="T">The type of object to parse.</typeparam>
    public class TryParseTypeReader<T> : TypeReader
    {
        public delegate bool TryParseDelegate<TA>(string input, out TA result);

        private readonly TryParseDelegate<T> tryParse;

        /// <summary>
        /// Initializes a new instance of the <see cref="TryParseTypeReader{T}"/> class.
        /// </summary>
        /// <param name="tryParse">The delegate representing the custom TryParse method for type T.</param>
        public TryParseTypeReader(TryParseDelegate<T> tryParse) => this.tryParse = tryParse;

        /// <inheritdoc />
        public override Task<TypeReaderResult> ReadAsync(
            ICommandContext context, string input, IServiceProvider services) =>
            tryParse(input, out var result)
                ? Task.FromResult(TypeReaderResult.FromSuccess(result))
                : Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Invalid input"));
    }

    /// <summary>
    /// Type reader for parsing input strings into enum values of type T using Enum.TryParse.
    /// </summary>
    /// <typeparam name="T">The enum type to parse.</typeparam>
    public class EnumTryParseTypeReader<T> : TypeReader where T : struct, Enum
    {
        private readonly bool ignoreCase;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumTryParseTypeReader{T}"/> class.
        /// </summary>
        /// <param name="ignoreCase">A boolean indicating whether to ignore case when parsing.</param>
        public EnumTryParseTypeReader(bool ignoreCase = true) => this.ignoreCase = ignoreCase;

        /// <inheritdoc />
        public override Task<TypeReaderResult> ReadAsync(
            ICommandContext context, string input, IServiceProvider services) =>
            Enum.TryParse<T>(input, ignoreCase, out var result) && Enum.IsDefined(result)
                ? Task.FromResult(TypeReaderResult.FromSuccess(result))
                : Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, $"Failed to parse {input}."));
    }
}