using Discord.Commands;
using Discord.WebSocket;
// ReSharper disable UnusedTypeParameter

namespace Mewdeko.Common.TypeReaders;

public abstract class MewdekoTypeReader<T> : TypeReader
{
    // ReSharper disable once NotAccessedField.Local
    private readonly DiscordSocketClient _client;
    // ReSharper disable once NotAccessedField.Local
    private readonly CommandService _cmds;

    protected MewdekoTypeReader(DiscordSocketClient client, CommandService cmds)
    {
        _client = client;
        _cmds = cmds;
    }
}