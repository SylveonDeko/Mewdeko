using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;

namespace Mewdeko.Common.TypeReaders;

public class ModuleTypeReader : MewdekoTypeReader<ModuleInfo>
{
    private readonly CommandService _cmds;

    public ModuleTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds) => _cmds = cmds;

    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
    {
        input = input.ToUpperInvariant();
        var module = _cmds.Modules.GroupBy(m => m.GetTopLevelModule())
            .FirstOrDefault(m => m.Key.Name.ToUpperInvariant() == input)?.Key;
        if (module == null)
            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "No such module found."));

        return Task.FromResult(TypeReaderResult.FromSuccess(module));
    }
}

public class ModuleOrCrTypeReader : MewdekoTypeReader<ModuleOrCrInfo>
{
    private readonly CommandService _cmds;

    public ModuleOrCrTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds) => _cmds = cmds;

    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
    {
        input = input.ToUpperInvariant();
        var module = _cmds.Modules.GroupBy(m => m.GetTopLevelModule())
            .FirstOrDefault(m => m.Key.Name.ToUpperInvariant() == input)?.Key;
        if (module == null && input != "ACTUALCUSTOMREACTIONS")
            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "No such module found."));

        return Task.FromResult(TypeReaderResult.FromSuccess(new ModuleOrCrInfo
        {
            Name = input
        }));
    }
}

public class ModuleOrCrInfo
{
    public string Name { get; set; }
}