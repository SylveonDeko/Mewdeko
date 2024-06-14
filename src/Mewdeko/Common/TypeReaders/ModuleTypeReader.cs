using Discord.Commands;

namespace Mewdeko.Common.TypeReaders
{
    /// <summary>
    /// Type reader for parsing module inputs into ModuleInfo objects.
    /// </summary>
    public class ModuleTypeReader : MewdekoTypeReader<ModuleInfo>
    {
        private readonly CommandService cmds;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleTypeReader"/> class.
        /// </summary>
        /// <param name="client">The DiscordShardedClient instance.</param>
        /// <param name="cmds">The CommandService instance.</param>
        public ModuleTypeReader(DiscordShardedClient client, CommandService cmds) : base(client, cmds) =>
            this.cmds = cmds;

        /// <inheritdoc />
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
        {
            input = input.ToUpperInvariant();
            var module = cmds.Modules.GroupBy(m => m.GetTopLevelModule())
                .FirstOrDefault(m => m.Key.Name.ToUpperInvariant() == input)?.Key;
            return Task.FromResult(module == null
                ? TypeReaderResult.FromError(CommandError.ParseFailed, "No such module found.")
                : TypeReaderResult.FromSuccess(module));
        }
    }

    /// <summary>
    /// Type reader for parsing module or custom reaction inputs into ModuleOrCrInfo objects.
    /// </summary>
    public class ModuleOrCrTypeReader : MewdekoTypeReader<ModuleOrCrInfo>
    {
        private readonly CommandService cmds;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleOrCrTypeReader"/> class.
        /// </summary>
        /// <param name="client">The DiscordShardedClient instance.</param>
        /// <param name="cmds">The CommandService instance.</param>
        public ModuleOrCrTypeReader(DiscordShardedClient client, CommandService cmds) : base(client, cmds) =>
            this.cmds = cmds;

        /// <inheritdoc />
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
        {
            input = input.ToUpperInvariant();
            var module = cmds.Modules.GroupBy(m => m.GetTopLevelModule())
                .FirstOrDefault(m => m.Key.Name.ToUpperInvariant() == input)?.Key;
            if (module == null && input != "ACTUALCUSTOMREACTIONS")
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "No such module found."));

            return Task.FromResult(TypeReaderResult.FromSuccess(new ModuleOrCrInfo
            {
                Name = input
            }));
        }
    }

    /// <summary>
    /// Represents information about a module or custom reaction.
    /// </summary>
    public class ModuleOrCrInfo
    {
        /// <summary>
        /// Gets or sets the name of the module or custom reaction.
        /// </summary>
        public string? Name { get; set; }
    }
}