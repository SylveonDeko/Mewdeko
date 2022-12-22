using System.Diagnostics;
using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Modules.Chat_Triggers.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.TypeReaders;

public class CommandTypeReader : MewdekoTypeReader<CommandInfo>
{
    public CommandTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
    {
    }

    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
        IServiceProvider services)
    {
        var cmds = services.GetService<CommandService>();
        var guildSettingsService = services.GetService<GuildSettingsService>();
        input = input.ToUpperInvariant();
        var prefix = await guildSettingsService?.GetPrefix(context.Guild);
        if (input.StartsWith(prefix?.ToUpperInvariant()!))
            input = input[prefix.Length..];
        var cmd = cmds?.Commands.FirstOrDefault(c =>
            c.Aliases.Select(a => a.ToUpperInvariant()).Contains(input));
        return cmd == null ? TypeReaderResult.FromError(CommandError.ParseFailed, "No such command found.") : TypeReaderResult.FromSuccess(cmd);
    }
}

public class CommandOrCrTypeReader : MewdekoTypeReader<CommandOrCrInfo>
{
    private readonly DiscordSocketClient client;
    private readonly CommandService cmds;

    public CommandOrCrTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
    {
        this.client = client;
        this.cmds = cmds;
    }

    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
        IServiceProvider services)
    {
        input = input.ToUpperInvariant();

        var crs = services.GetService<ChatTriggersService>();

        Debug.Assert(crs != null, $"{nameof(crs)} != null");
        if (await crs.ReactionExists(context.Guild?.Id, input))
            return TypeReaderResult.FromSuccess(new CommandOrCrInfo(input, CommandOrCrInfo.Type.Custom));

        var cmd = await new CommandTypeReader(client, cmds).ReadAsync(context, input, services)
            .ConfigureAwait(false);
        if (cmd.IsSuccess)
        {
            return TypeReaderResult.FromSuccess(new CommandOrCrInfo(((CommandInfo)cmd.Values.First().Value).Name,
                CommandOrCrInfo.Type.Normal));
        }

        return TypeReaderResult.FromError(CommandError.ParseFailed, "No such command or custom reaction found.");
    }
}

public class CommandOrCrInfo
{
    public enum Type
    {
        Normal,
        Custom
    }

    public CommandOrCrInfo(string input, Type type)
    {
        Name = input;
        CmdType = type;
    }

    public string Name { get; set; }
    public Type CmdType { get; set; }
    public bool IsCustom => CmdType == Type.Custom;
}