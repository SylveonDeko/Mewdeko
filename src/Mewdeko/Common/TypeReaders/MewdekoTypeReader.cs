using Discord.Commands;

// ReSharper disable UnusedTypeParameter

namespace Mewdeko.Common.TypeReaders
{
    /// <summary>
    /// Abstract base class for implementing custom type readers.
    /// </summary>
    /// <typeparam name="T">The type that the type reader converts input to.</typeparam>
    public abstract class MewdekoTypeReader<T> : TypeReader
    {
        // DiscordShardedClient instance (unused field)
        // ReSharper disable once NotAccessedField.Local
        private readonly DiscordShardedClient client;

        // CommandService instance (unused field)
        // ReSharper disable once NotAccessedField.Local
        private readonly CommandService cmds;

        /// <summary>
        /// Initializes a new instance of the <see cref="MewdekoTypeReader{T}"/> class.
        /// </summary>
        /// <param name="client">The DiscordShardedClient instance.</param>
        /// <param name="cmds">The CommandService instance.</param>
        protected MewdekoTypeReader(DiscordShardedClient client, CommandService cmds)
        {
            this.client = client;
            this.cmds = cmds;
        }
    }
}