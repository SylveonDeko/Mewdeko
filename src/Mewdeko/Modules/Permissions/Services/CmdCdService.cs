using Discord.Commands;
using Discord.Interactions;
using LinqToDB.Async;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using CommandInfo = Discord.Commands.CommandInfo;

namespace Mewdeko.Modules.Permissions.Services;

/// <summary>
///     Represents a service for managing command cooldowns.
/// </summary>
public class CmdCdService : ILateBlocker, INService
{
    private readonly IDataConnectionFactory dbFactory;

    private readonly SlashCommandIdentityService identity;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CmdCdService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    public CmdCdService(IDataConnectionFactory dbFactory, SlashCommandIdentityService identity)
    {
        this.dbFactory = dbFactory;
        this.identity = identity;
    }

    /// <summary>
    ///     Tracks active cooldowns for commands being executed by users across different guilds.
    /// </summary>
    /// <remarks>
    ///     This dictionary maps guild IDs to a set of <see cref="ActiveCooldown" /> objects,
    ///     indicating commands that are currently under cooldown for users within each guild.
    ///     It prevents users from spamming commands by enforcing cooldown periods after a command's execution.
    /// </remarks>
    public ConcurrentDictionary<ulong, ConcurrentHashSet<ActiveCooldown>> ActiveCooldowns { get; } = new();

    /// <summary>
    ///     The priority order in which the early behavior should run, with lower numbers indicating higher priority.
    /// </summary>
    public int Priority { get; } = 0;

    /// <summary>
    ///     Attempts to block a command execution based on the command's cooldown status for a given user in the context of
    ///     traditional command interactions.
    /// </summary>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="ctx">The command context which includes information about the user, guild, and channel.</param>
    /// <param name="moduleName">The name of the module containing the command.</param>
    /// <param name="command">Information about the command being executed.</param>
    /// <returns>A task that resolves to true if the command execution should be blocked due to cooldown; otherwise, false.</returns>
    /// <remarks>
    ///     This method checks if there's an existing cooldown for the command being executed by the user in the specified
    ///     guild.
    ///     If the command is on cooldown, the execution is blocked. This check is specific to traditional text-based commands.
    /// </remarks>
    public async Task<bool> TryBlockLate(DiscordShardedClient client, ICommandContext ctx, string moduleName,
        CommandInfo command)
    {
        var guild = ctx.Guild;
        var user = ctx.User;
        var commandName = command.MethodName().ToLowerInvariant();

        return await TryBlock(guild, user, commandName);
    }

    /// <summary>
    ///     Attempts to block a command execution based on the command's cooldown status for a given user in the context of
    ///     interaction-based commands.
    /// </summary>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="ctx">The interaction context which includes information about the user, guild, and channel.</param>
    /// <param name="command">Information about the interaction command being executed.</param>
    /// <returns>A task that resolves to true if the command execution should be blocked due to cooldown; otherwise, false.</returns>
    /// <remarks>
    ///     This method performs a similar function to
    ///     <see cref="TryBlockLate(DiscordShardedClient, ICommandContext, string, CommandInfo)" />
    ///     but is tailored for interactions such as slash commands. It checks if the interaction command is on cooldown for
    ///     the user.
    /// </remarks>
    public async Task<bool> TryBlockLate(DiscordShardedClient client, IInteractionContext ctx,
        ICommandInfo command)
    {
        var guild = ctx.Guild;
        var user = ctx.User;
        var commandName = identity.Resolve(command).MethodName;

        return await TryBlock(guild, user, commandName);
    }

    /// <summary>
    ///     Core method for checking and applying command cooldowns to prevent command execution if the command is currently on
    ///     cooldown for the user.
    /// </summary>
    /// <param name="guild">The guild in which the command was issued, may be null for direct messages.</param>
    /// <param name="user">The user who issued the command.</param>
    /// <param name="commandName">The name of the command to check the cooldown for.</param>
    /// <returns>A task that resolves to true if the command is currently on cooldown for the user; otherwise, false.</returns>
    /// <remarks>
    ///     Checks if the specified command is on cooldown for the user in the given guild. If so, blocks the command
    ///     execution.
    ///     This method manages cooldown state and ensures that commands cannot be spammed by tracking active cooldowns.
    /// </remarks>
    public async Task<bool> TryBlock(IGuild? guild, IUser user, string commandName)
    {
        if (guild is null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Query CommandCooldowns with GuildId filter
        var cdRule = await db.CommandCooldowns
            .FirstOrDefaultAsync(cc => cc.GuildId == guild.Id && cc.CommandName == commandName);

        if (cdRule == null)
            return false;

        var activeCdsForGuild = ActiveCooldowns.GetOrAdd(guild.Id, []);
        if (activeCdsForGuild.FirstOrDefault(ac => ac.UserId == user.Id && ac.Command == commandName) != null)
            return true;

        activeCdsForGuild.Add(new ActiveCooldown
        {
            UserId = user.Id, Command = commandName
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(cdRule.Seconds * 1000).ConfigureAwait(false);
                activeCdsForGuild.RemoveWhere(ac => ac.Command == commandName && ac.UserId == user.Id);
            }
            catch
            {
                // ignored
            }
        });

        return false;
    }
}

/// <summary>
///     Represents an active cooldown period for a command issued by a specific user.
/// </summary>
/// <remarks>
///     This class is used to track cooldowns on command usage to prevent spamming. Each instance corresponds to a command
///     currently under cooldown for a user. It stores both the command that's cooling down and the ID of the user who
///     triggered it.
/// </remarks>
public class ActiveCooldown
{
    /// <summary>
    ///     Gets or sets the name of the command under cooldown.
    /// </summary>
    /// <value>The name of the command.</value>
    public string Command { get; set; }

    /// <summary>
    ///     Gets or sets the user ID of the user who initiated the command, subjecting it to the cooldown.
    /// </summary>
    /// <value>The Discord user ID.</value>
    public ulong UserId { get; set; }
}