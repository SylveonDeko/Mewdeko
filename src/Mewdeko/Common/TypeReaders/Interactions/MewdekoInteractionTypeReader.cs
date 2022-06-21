using Discord.Interactions;

// ReSharper disable UnusedTypeParameter

namespace Mewdeko.Common.TypeReaders.Interactions;

public abstract class MewdekoTypeReader<T> : TypeReader
{
    // ReSharper disable once NotAccessedField.Local
    private readonly DiscordSocketClient _client;
    // ReSharper disable once NotAccessedField.Local
    private readonly InteractionService _cmds;

    protected MewdekoTypeReader(DiscordSocketClient client, InteractionService cmds)
    {
        _client = client;
        _cmds = cmds;
    }
}