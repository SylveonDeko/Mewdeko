using System.Diagnostics;
using Discord.Commands;
using Mewdeko.Modules.Chat_Triggers.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.TypeReaders;

/// <summary>
///     Type reader for parsing command inputs into CommandInfo objects.
/// </summary>
public class CommandTypeReader : MewdekoTypeReader<CommandInfo>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandTypeReader" /> class.
    /// </summary>
    /// <param name="client">The discord client</param>
    /// <param name="cmds">The command service</param>
    public CommandTypeReader(DiscordShardedClient client, CommandService cmds) : base(client, cmds)
    {
    }

    /// <inheritdoc />
    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
        IServiceProvider services)
    {
        var cmds = services.GetService<CommandService>(); // Retrieves the CommandService instance from services
        var guildSettingsService =
            services
                .GetService<GuildSettingsService>(); // Retrieves the GuildSettingsService instance from services

        input = input.ToUpperInvariant(); // Converts the input string to uppercase for case-insensitive comparison

        var prefix =
            await guildSettingsService
                ?.GetPrefix(context.Guild); // Retrieves the command prefix from guild settings
        if (input.StartsWith(prefix?.ToUpperInvariant()!)) // Removes the command prefix from the input if present
            input = input[prefix.Length..];

        // Finds the command from the command service based on input aliases
        var cmd = cmds?.Commands.FirstOrDefault(c =>
            c.Aliases.Select(a => a.ToUpperInvariant()).Contains(input));

        // Returns TypeReaderResult based on whether command is found or not
        return cmd == null
            ? TypeReaderResult.FromError(CommandError.ParseFailed, "No such command found.")
            : TypeReaderResult.FromSuccess(cmd);
    }
}

/// <summary>
///     Type reader for parsing custom commands or reactions into CommandOrCrInfo objects.
/// </summary>
public class CommandOrCrTypeReader : MewdekoTypeReader<CommandOrCrInfo>
{
    private readonly DiscordShardedClient client;
    private readonly CommandService cmds;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandOrCrTypeReader" /> class.
    /// </summary>
    /// <param name="client">The discord client</param>
    /// <param name="cmds">The command service</param>
    public CommandOrCrTypeReader(DiscordShardedClient client, CommandService cmds) : base(client, cmds)
    {
        this.client = client;
        this.cmds = cmds;
    }

    /// <inheritdoc />
    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
        IServiceProvider services)
    {
        var crs = services
            .GetService<ChatTriggersService>(); // Retrieves the ChatTriggersService instance from services

        Debug.Assert(crs != null, $"{nameof(crs)} != null");

        // Trigger text is stored lower-cased, so the lookup has to be case-insensitive rather than upper-casing
        // the input the way the plain command lookup below does.
        var triggers = await crs.GetChatTriggersFor(context.Guild.Id);
        var trigger = int.TryParse(input, out var id)
            ? triggers.FirstOrDefault(x => x.Id == id)
            : triggers.FirstOrDefault(x =>
                string.Equals(x.Trigger, input, StringComparison.InvariantCultureIgnoreCase));

        // Checks if the input matches any custom reaction
        if (trigger is not null)
        {
            return TypeReaderResult.FromSuccess(new CommandOrCrInfo(trigger.Trigger!, CommandOrCrInfo.Type.Custom,
                trigger.Id.ToString()));
        }

        // Parses the input as a command if not a custom reaction
        var cmd = await new CommandTypeReader(client, cmds).ReadAsync(context, input, services)
            .ConfigureAwait(false);

        // Returns TypeReaderResult based on whether a command or custom reaction is found or not
        if (cmd.IsSuccess)
        {
            return TypeReaderResult.FromSuccess(new CommandOrCrInfo(((CommandInfo)cmd.Values.First().Value).Name,
                CommandOrCrInfo.Type.Normal));
        }

        return TypeReaderResult.FromError(CommandError.ParseFailed, "No such command or custom reaction found.");
    }
}

/// <summary>
///     Represents information about a command or a custom reaction.
/// </summary>
public class CommandOrCrInfo
{
    /// <summary>
    ///     Specifies the type of the command or custom reaction.
    /// </summary>
    public enum Type
    {
        /// <summary>
        ///     Indicates a normal command.
        /// </summary>
        Normal,

        /// <summary>
        ///     Indicates a chat trigger.
        /// </summary>
        Custom
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandOrCrInfo" /> class.
    /// </summary>
    /// <param name="input">The name of the command or custom reaction.</param>
    /// <param name="type">The type of the command or custom reaction.</param>
    /// <param name="permKey">
    ///     The key permission entries are stored under. Chat triggers use their numeric id so that permissions
    ///     survive a rename; defaults to <paramref name="input" />.
    /// </param>
    public CommandOrCrInfo(string input, Type type, string? permKey = null)
    {
        Name = input;
        CmdType = type;
        PermKey = permKey ?? input;
    }

    /// <summary>
    ///     Gets or sets the name of the command or custom reaction.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the type of the command or custom reaction.
    /// </summary>
    public Type CmdType { get; set; }

    /// <summary>
    ///     Indicates whether the command or custom reaction is a custom reaction.
    /// </summary>
    public bool IsCustom
    {
        get
        {
            return CmdType == Type.Custom;
        }
    }

    /// <summary>
    ///     Gets the key that permission entries are stored under. For chat triggers this is the trigger id, so that
    ///     renaming the trigger does not orphan its permissions; for normal commands it is the command name.
    /// </summary>
    public string PermKey { get; set; }
}