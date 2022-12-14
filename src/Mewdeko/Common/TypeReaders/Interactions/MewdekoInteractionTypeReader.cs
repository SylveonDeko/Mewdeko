using Discord.Interactions;

// ReSharper disable UnusedTypeParameter

namespace Mewdeko.Common.TypeReaders.Interactions;

public abstract class MewdekoTypeReader<T> : TypeReader
{
    // ReSharper disable once NotAccessedField.Local
    private readonly DiscordSocketClient client;

    // ReSharper disable once NotAccessedField.Local
    private readonly InteractionService cmds;

    protected MewdekoTypeReader(DiscordSocketClient client, InteractionService cmds)
    {
        this.client = client;
        this.cmds = cmds;
    }
}