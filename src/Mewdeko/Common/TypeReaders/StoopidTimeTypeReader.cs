using Discord.Commands;
using Mewdeko.Common.TypeReaders.Models;

namespace Mewdeko.Common.TypeReaders
{
    /// <summary>
    /// Type reader for parsing StoopidTime inputs into StoopidTime objects.
    /// </summary>
    public class StoopidTimeTypeReader : MewdekoTypeReader<StoopidTime>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StoopidTimeTypeReader"/> class.
        /// </summary>
        /// <param name="client">The DiscordShardedClient instance.</param>
        /// <param name="cmds">The CommandService instance.</param>
        public StoopidTimeTypeReader(DiscordShardedClient client, CommandService cmds) : base(client, cmds)
        {
        }

        /// <inheritdoc />
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
            IServiceProvider services)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(TypeReaderResult.FromError(CommandError.Unsuccessful,
                    "Input is empty.")); // Returns error if input is empty
            try
            {
                var time = StoopidTime.FromInput(input); // Parses input string into StoopidTime object
                return
                    Task.FromResult(TypeReaderResult
                        .FromSuccess(time)); // Returns successfully parsed StoopidTime object
            }
            catch (Exception ex)
            {
                return Task.FromResult(TypeReaderResult.FromError(CommandError.Exception,
                    ex.Message)); // Returns error if parsing fails
            }
        }
    }
}