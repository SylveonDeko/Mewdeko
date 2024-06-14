using System.Diagnostics;
using Discord.Commands;
using Mewdeko.Modules.Chat_Triggers.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.TypeReaders
{
    /// <summary>
    /// Type reader for parsing command inputs into CommandInfo objects.
    /// </summary>
    public class CommandTypeReader : MewdekoTypeReader<CommandInfo>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandTypeReader"/> class.
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
    /// Type reader for parsing custom commands or reactions into CommandOrCrInfo objects.
    /// </summary>
    public class CommandOrCrTypeReader : MewdekoTypeReader<CommandOrCrInfo>
    {
        private readonly DiscordShardedClient client;
        private readonly CommandService cmds;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandOrCrTypeReader"/> class.
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
            input = input.ToUpperInvariant(); // Converts the input string to uppercase for case-insensitive comparison

            var crs = services
                .GetService<ChatTriggersService>(); // Retrieves the ChatTriggersService instance from services

            Debug.Assert(crs != null, $"{nameof(crs)} != null");

            // Checks if the input matches any custom reaction
            if (await crs.ReactionExists(context.Guild?.Id, input))
                return TypeReaderResult.FromSuccess(new CommandOrCrInfo(input, CommandOrCrInfo.Type.Custom));

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
    /// Represents information about a command or a custom reaction.
    /// </summary>
    public class CommandOrCrInfo
    {
        /// <summary>
        /// Specifies the type of the command or custom reaction.
        /// </summary>
        public enum Type
        {
            /// <summary>
            /// Indicates a normal command.
            /// </summary>
            Normal,

            /// <summary>
            /// Indicates a chat trigger.
            /// </summary>
            Custom
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandOrCrInfo"/> class.
        /// </summary>
        /// <param name="input">The name of the command or custom reaction.</param>
        /// <param name="type">The type of the command or custom reaction.</param>
        public CommandOrCrInfo(string input, Type type)
        {
            Name = input;
            CmdType = type;
        }

        /// <summary>
        /// Gets or sets the name of the command or custom reaction.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type of the command or custom reaction.
        /// </summary>
        public Type CmdType { get; set; }

        /// <summary>
        /// Indicates whether the command or custom reaction is a custom reaction.
        /// </summary>
        public bool IsCustom => CmdType == Type.Custom;
    }
}