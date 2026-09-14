using DataModel;
using Discord.Commands;
using Discord.Interactions;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Strings;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Reputation.Services;

/// <summary>
///     Service that integrates reputation requirements with Mewdeko's existing permission system.
/// </summary>
public class RepCommandRequirementsService : INService, ILateBlocker
{
    // Cache for command requirements to avoid database hits
    private readonly ConcurrentDictionary<(ulong guildId, string command), RepCommandRequirement?>
        commandRequirementsCache = new();

    private readonly BotConfig config;
    private readonly IDataConnectionFactory dbFactory;
    private readonly SlashCommandIdentityService identity;
    private readonly ILogger<RepCommandRequirementsService> logger;
    private readonly RepService repService;
    private readonly GeneratedBotStrings strings;
    private DateTime lastCacheUpdate = DateTime.MinValue;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RepCommandRequirementsService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="strings">The localized bot strings.</param>
    /// <param name="repService">The reputation service.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="config">The bot config class</param>
    public RepCommandRequirementsService(
        IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings,
        RepService repService,
        ILogger<RepCommandRequirementsService> logger, BotConfig config, SlashCommandIdentityService identity)
    {
        this.identity = identity;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.repService = repService;
        this.logger = logger;
        this.config = config;
    }

    /// <summary>
    ///     Gets the priority for the late blocker. Lower numbers = higher priority.
    /// </summary>
    public int Priority
    {
        get
        {
            return 10;
            // Run after normal permissions but before most other blockers
        }
    }

    /// <summary>
    ///     Checks if a user meets the reputation requirements for a specific command.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="ctx">The command context.</param>
    /// <param name="moduleName">The module name.</param>
    /// <param name="command">The command info.</param>
    /// <returns>True if the command should be blocked, false if it can proceed.</returns>
    public async Task<bool> TryBlockLate(DiscordShardedClient client, ICommandContext ctx, string moduleName,
        CommandInfo command)
    {
        if (ctx.Guild == null) return false;

        try
        {
            var requirement = await GetCommandRequirementInternalAsync(ctx.Guild.Id, command.Name);
            if (requirement is not { IsActive: true }) return false;

            // Check if user has bypass roles
            if (await HasBypassRoleAsync(ctx.User as IGuildUser, requirement.BypassRoles))
                return false;

            // Check if command is restricted to specific channels
            if (!string.IsNullOrEmpty(requirement.RestrictedChannels) &&
                !IsChannelInList(ctx.Channel.Id, requirement.RestrictedChannels))
                return false;

            // Check user's reputation
            var hasRequirement = await CheckUserReputationRequirementAsync(ctx.Guild.Id, ctx.User.Id,
                requirement.MinReputation, requirement.RequiredRepType);

            if (!hasRequirement)
            {
                var message = requirement.DenialMessage ??
                              strings.RepCommandRequirementNotMet(ctx.Guild.Id, requirement.MinReputation,
                                  requirement.RequiredRepType ?? "total");
                await ctx.Channel.SendErrorAsync(message, config);
                return true; // Block the command
            }

            return false; // Allow the command
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking command requirements for {Command} in guild {GuildId}",
                command.Name, ctx.Guild.Id);
            return false; // Allow command on error to prevent blocking legitimate usage
        }
    }

    /// <summary>
    ///     Checks if a user meets the reputation requirements for a slash command.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="command">The command info.</param>
    /// <returns>True if the command should be blocked, false if it can proceed.</returns>
    public async Task<bool> TryBlockLate(DiscordShardedClient client, IInteractionContext ctx, ICommandInfo command)
    {
        if (ctx.Guild == null) return false;

        try
        {
            var requirement =
                await GetCommandRequirementInternalAsync(ctx.Guild.Id, identity.Resolve(command).Alias);
            if (requirement is not { IsActive: true }) return false;

            // Check if user has bypass roles
            if (await HasBypassRoleAsync(ctx.User as IGuildUser, requirement.BypassRoles))
                return false;

            // Check if command is restricted to specific channels
            if (!string.IsNullOrEmpty(requirement.RestrictedChannels) &&
                !IsChannelInList(ctx.Channel.Id, requirement.RestrictedChannels))
                return false;

            // Check user's reputation
            var hasRequirement = await CheckUserReputationRequirementAsync(ctx.Guild.Id, ctx.User.Id,
                requirement.MinReputation, requirement.RequiredRepType);

            if (!hasRequirement)
            {
                var message = requirement.DenialMessage ??
                              strings.RepCommandRequirementNotMet(ctx.Guild.Id, requirement.MinReputation,
                                  requirement.RequiredRepType ?? "total");
                await ctx.Interaction.RespondAsync(message, ephemeral: true);
                return true; // Block the command
            }

            return false; // Allow the command
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking slash command requirements for {Command} in guild {GuildId}",
                command.MethodName, ctx.Guild.Id);
            return false; // Allow command on error to prevent blocking legitimate usage
        }
    }

    /// <summary>
    ///     Adds or updates a command requirement.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="commandName">The command name.</param>
    /// <param name="minReputation">The minimum reputation required.</param>
    /// <param name="repType">The specific reputation type required.</param>
    /// <param name="restrictedChannels">Channels where this applies.</param>
    /// <param name="denialMessage">Custom denial message.</param>
    /// <param name="bypassRoles">Roles that bypass this requirement.</param>
    /// <param name="showInHelp">Whether to show in help.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddCommandRequirementAsync(ulong guildId, string commandName, int minReputation,
        string? repType = null, string? restrictedChannels = null, string? denialMessage = null,
        string? bypassRoles = null, bool showInHelp = true)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.RepCommandRequirements
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.CommandName == commandName);

        if (existing != null)
        {
            existing.MinReputation = minReputation;
            existing.RequiredRepType = repType;
            existing.RestrictedChannels = restrictedChannels;
            existing.DenialMessage = denialMessage;
            existing.BypassRoles = bypassRoles;
            existing.ShowInHelp = showInHelp;
            existing.IsActive = true;

            await db.UpdateAsync(existing);
        }
        else
        {
            var requirement = new RepCommandRequirement
            {
                GuildId = guildId,
                CommandName = commandName,
                MinReputation = minReputation,
                RequiredRepType = repType,
                RestrictedChannels = restrictedChannels,
                DenialMessage = denialMessage,
                BypassRoles = bypassRoles,
                ShowInHelp = showInHelp,
                IsActive = true
            };

            await db.InsertAsync(requirement);
        }

        // Clear cache
        commandRequirementsCache.TryRemove((guildId, commandName), out _);
    }

    /// <summary>
    ///     Removes a command requirement.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="commandName">The command name.</param>
    /// <returns>The number of deleted entries.</returns>
    public async Task<int> RemoveCommandRequirementAsync(ulong guildId, string commandName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.RepCommandRequirements
            .Where(x => x.GuildId == guildId && x.CommandName == commandName)
            .DeleteAsync();

        commandRequirementsCache.TryRemove((guildId, commandName), out _);

        return deleted;
    }

    /// <summary>
    ///     Gets all command requirements for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>List of command requirements.</returns>
    public async Task<List<RepCommandRequirement>> GetCommandRequirementsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.RepCommandRequirements
            .Where(x => x.GuildId == guildId && x.IsActive)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets a specific command requirement for a guild and command.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="commandName">The command name.</param>
    /// <returns>The command requirement or null if not found.</returns>
    public async Task<RepCommandRequirement?> GetCommandRequirementAsync(ulong guildId, string commandName)
    {
        return await GetCommandRequirementInternalAsync(guildId, commandName);
    }

    /// <summary>
    ///     Gets a command requirement from cache or database.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="commandName">The command name.</param>
    /// <returns>The command requirement or null if not found.</returns>
    private async Task<RepCommandRequirement?> GetCommandRequirementInternalAsync(ulong guildId, string commandName)
    {
        var key = (guildId, commandName.ToLowerInvariant());

        if (commandRequirementsCache.TryGetValue(key, out var cached) &&
            DateTime.UtcNow - lastCacheUpdate < TimeSpan.FromMinutes(5))
        {
            return cached;
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var requirement = await db.RepCommandRequirements
            .FirstOrDefaultAsync(x =>
                x.GuildId == guildId && x.CommandName == commandName.ToLowerInvariant() && x.IsActive);

        commandRequirementsCache.TryAdd(key, requirement);
        lastCacheUpdate = DateTime.UtcNow;

        return requirement;
    }

    /// <summary>
    ///     Checks if a user meets the reputation requirement.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="minReputation">The minimum reputation required.</param>
    /// <param name="repType">The specific reputation type required.</param>
    /// <returns>True if the user meets the requirement.</returns>
    private async Task<bool> CheckUserReputationRequirementAsync(ulong guildId, ulong userId, int minReputation,
        string? repType)
    {
        if (string.IsNullOrEmpty(repType))
        {
            var (totalRep, _) = await repService.GetUserReputationAsync(guildId, userId);
            return totalRep >= minReputation;
        }

        var customRep = await repService.GetUserCustomReputationAsync(guildId, userId, repType);
        return customRep >= minReputation;
    }

    /// <summary>
    ///     Checks if a user has bypass roles.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="bypassRoles">The bypass roles JSON string.</param>
    /// <returns>True if the user has bypass roles.</returns>
    private static async Task<bool> HasBypassRoleAsync(IGuildUser? user, string? bypassRoles)
    {
        await Task.CompletedTask;
        if (user == null || string.IsNullOrEmpty(bypassRoles)) return false;

        try
        {
            var roleIds = JsonConvert.DeserializeObject<List<ulong>>(bypassRoles);
            return roleIds != null && user.RoleIds.Any(roleId => roleIds.Contains(roleId));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Checks if a channel is in the restricted channels list.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="restrictedChannels">The restricted channels JSON string.</param>
    /// <returns>True if the channel is in the list.</returns>
    private static bool IsChannelInList(ulong channelId, string restrictedChannels)
    {
        try
        {
            var channelIds = JsonConvert.DeserializeObject<List<ulong>>(restrictedChannels);
            return channelIds?.Contains(channelId) ?? false;
        }
        catch
        {
            return false;
        }
    }
}