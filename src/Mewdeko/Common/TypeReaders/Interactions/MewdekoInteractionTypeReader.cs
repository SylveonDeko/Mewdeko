using Discord.Interactions;

// ReSharper disable UnusedTypeParameter

namespace Mewdeko.Common.TypeReaders.Interactions;

/// <summary>
/// Abstract class that provides a base for type readers in the Mewdeko application.
/// </summary>
/// <typeparam name="T">The type that the derived class will read.</typeparam>
public abstract class MewdekoTypeReader<T> : TypeReader
{
    /// <summary>
    /// The Discord client.
    /// </summary>
    // ReSharper disable once NotAccessedField.Local
    private readonly DiscordShardedClient client;

    /// <summary>
    /// The interaction service.
    /// </summary>
    // ReSharper disable once NotAccessedField.Local
    private readonly InteractionService cmds;

    /// <summary>
    /// Initializes a new instance of the MewdekoTypeReader class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="cmds">The interaction service.</param>
    protected MewdekoTypeReader(DiscordShardedClient client, InteractionService cmds)
    {
        this.client = client;
        this.cmds = cmds;
    }
}