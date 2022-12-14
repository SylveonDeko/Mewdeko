using Discord.Commands;

// ReSharper disable UnusedTypeParameter

namespace Mewdeko.Common.TypeReaders;

public abstract class MewdekoTypeReader<T> : TypeReader
{
    // ReSharper disable once NotAccessedField.Local
    private readonly DiscordSocketClient client;

    // ReSharper disable once NotAccessedField.Local
    private readonly CommandService cmds;

    protected MewdekoTypeReader(DiscordSocketClient client, CommandService cmds)
    {
        this.client = client;
        this.cmds = cmds;
    }
}