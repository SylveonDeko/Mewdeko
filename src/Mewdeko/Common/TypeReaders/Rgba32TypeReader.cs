using Discord.Commands;
using SkiaSharp;

namespace Mewdeko.Common.TypeReaders
{
    /// <summary>
    /// Type reader for parsing SKColor inputs into SKColor objects.
    /// </summary>
    public class SkColorTypeReader : MewdekoTypeReader<SKColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SkColorTypeReader"/> class.
        /// </summary>
        /// <param name="client">The DiscordShardedClient instance.</param>
        /// <param name="cmds">The CommandService instance.</param>
        public SkColorTypeReader(DiscordShardedClient client, CommandService cmds) : base(client, cmds)
        {
        }

        /// <inheritdoc />
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
            IServiceProvider services)
        {
            await Task.Yield(); // Forces the method to run asynchronously

            input = input.Replace("#", "", StringComparison.InvariantCulture); // Removes '#' symbol from input
            try
            {
                return TypeReaderResult.FromSuccess(SKColor.Parse(input)); // Parses input string and returns SKColor
            }
            catch
            {
                return TypeReaderResult.FromError(CommandError.ParseFailed,
                    "Parameter is not a valid color hex or name."); // Returns error message if parsing fails
            }
        }
    }
}