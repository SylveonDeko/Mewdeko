using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Votes.Services;

/// <summary>
///     Manages voting functionality within the bot, handling vote configuration, processing, and reward distribution.
/// </summary>
public class VoteService : INService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly MuteService muteService;
    private readonly GeneratedBotStrings Strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VoteService" /> class, setting up dependencies and subscribing to
    ///     voting-related pub/sub events.
    /// </summary>
    /// <param name="dbFactory">The database service for data access.</param>
    /// <param name="client">The Discord client for interacting with the Discord API.</param>
    /// <param name="muteService">The service for managing mutes within the bot.</param>
    /// <param name="strings">The localized strings service.</param>
    public VoteService(IDataConnectionFactory dbFactory, DiscordShardedClient client,
        MuteService muteService, GeneratedBotStrings strings)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.muteService = muteService;
        Strings = strings;
    }

    /// <summary>
    ///     Vote password and data handler
    /// </summary>
    /// <param name="voteModal">The data the api returns</param>
    public async ValueTask RunVoteStuff(CompoundVoteModal voteModal)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get potential vote config using LinqToDB
        var potentialVoteConfig = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.VotesPassword == voteModal.Password);

        if (potentialVoteConfig is null)
            return;

        var guild = client.GetGuild(potentialVoteConfig.GuildId);
        if (guild is null)
            return;

        var user = guild.GetUser(ulong.Parse(voteModal.VoteModel.User));
        var newVote = new DataModel.Vote
        {
            UserId = user.Id, GuildId = guild.Id
        };

        // Insert new vote using LinqToDB
        await db.InsertAsync(newVote);

        if (string.IsNullOrEmpty(potentialVoteConfig.VoteEmbed))
        {
            if (potentialVoteConfig.VotesChannel is 0)
                return;

            if (guild.GetTextChannel(potentialVoteConfig.VotesChannel) is not ITextChannel channel)
                return;

            // Count votes using LinqToDB
            var votes = await db.Votes
                .CountAsync(x => x.UserId == user.Id && x.GuildId == guild.Id);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.VoteThanks(guild.Id, guild.Name))
                .WithDescription(Strings.VoteCountCorrect(guild.Id, votes))
                .WithThumbnailUrl(user.RealAvatarUrl().AbsoluteUri)
                .WithOkColor(guild.Id);

            await channel.SendMessageAsync(user.Mention, embed: eb.Build());

            // Check for vote roles using LinqToDB
            if (!await db.VoteRoles.AnyAsync(x => x.GuildId == guild.Id))
                return;

            // Get vote roles using LinqToDB
            var split = await db.VoteRoles
                .Where(x => x.GuildId == guild.Id)
                .ToListAsync();

            if (!split.Any())
                return;

            foreach (var i in split)
            {
                var role = guild.GetRole(i.RoleId);
                try
                {
                    await user.AddRoleAsync(role);
                }
                catch
                {
                    // ignored, means role was too high or inaccessible
                }

                if (i.Timer is 0) continue;
                var timespan = TimeSpan.FromSeconds(i.Timer);
                await muteService.TimedRole(user, timespan, "Vote Role", role);
            }
        }
        else
        {
            if (guild.GetTextChannel(potentialVoteConfig.VotesChannel) is not ITextChannel channel)
                return;

            if (potentialVoteConfig.VotesChannel is 0)
                return;

            // Get votes using LinqToDB
            var votesQuery = db.Votes
                .Where(x => x.UserId == user.Id && x.GuildId == guild.Id);

            var votesCount = await votesQuery.CountAsync();
            var votesMonthCount = await votesQuery
                .CountAsync(x => x.DateAdded.Value.Month == DateTime.UtcNow.Month);

            var rep = new ReplacementBuilder()
                .WithDefault(user, null, guild, client)
                .WithOverride("%votestotalcount%", () => votesCount.ToString())
                .WithOverride("%votesmonthcount%", () => votesMonthCount.ToString())
                .Build();

            if (SmartEmbed.TryParse(rep.Replace(potentialVoteConfig.VoteEmbed), guild.Id, out var embeds,
                    out var plainText, out var components))
            {
                await channel.SendMessageAsync(plainText, embeds: embeds, components: components.Build());
            }
            else
            {
                await channel.SendMessageAsync(rep.Replace(potentialVoteConfig.VoteEmbed).SanitizeMentions());
            }

            // Check for vote roles using LinqToDB
            if (!await db.VoteRoles.AnyAsync(x => x.GuildId == guild.Id))
                return;

            // Get vote roles using LinqToDB
            var split = await db.VoteRoles
                .Where(x => x.GuildId == guild.Id)
                .ToListAsync();

            if (!split.Any())
                return;

            foreach (var i in split)
            {
                var role = guild.GetRole(i.RoleId);
                try
                {
                    await user.AddRoleAsync(role);
                }
                catch
                {
                    // ignored, means role was too high or inaccessible
                }

                if (i.Timer is 0) continue;
                var timespan = TimeSpan.FromSeconds(i.Timer);
                await muteService.TimedRole(user, timespan, "Vote Role", role);
            }
        }
    }

    /// <summary>
    ///     Adds a vote role to the guild configuration, setting a timer for automatic role removal if specified.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the role is to be added.</param>
    /// <param name="roleId">The ID of the role to be added as a vote role.</param>
    /// <param name="seconds">
    ///     The duration in seconds after which the role should be automatically removed. Zero for
    ///     indefinite.
    /// </param>
    /// <returns>A tuple indicating success status and an optional error message.</returns>
    public async Task<(bool, string)> AddVoteRole(ulong guildId, ulong roleId, int seconds = 0)
    {
        if (roleId == guildId)
            return (false, Strings.UnableToAddEveryoneRole(guildId));

        await using var db = await dbFactory.CreateConnectionAsync();

        // Count vote roles using LinqToDB
        if (await db.VoteRoles.CountAsync(x => x.GuildId == guildId) >= 10)
            return (false, Strings.ReachedMaximumVoteRoles(guildId));

        // Check if role already exists as vote role using LinqToDB
        if (await db.VoteRoles
                .AnyAsync(x => x.GuildId == guildId && x.RoleId == roleId))
            return (false, Strings.RoleAlreadyVoteRole(guildId));

        var voteRole = new VoteRole
        {
            RoleId = roleId, GuildId = guildId, Timer = seconds
        };

        // Insert vote role using LinqToDB
        await db.InsertAsync(voteRole);

        return (true, null);
    }

    /// <summary>
    ///     Removes a vote role from the guild configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild from which the role is to be removed.</param>
    /// <param name="roleId">The ID of the role to be removed from vote roles.</param>
    /// <returns>A tuple indicating success status and an optional error message.</returns>
    public async Task<(bool, string)> RemoveVoteRole(ulong guildId, ulong roleId)
    {
        if (roleId == guildId)
            return (false, Strings.UnableToAddEveryoneRole(guildId));

        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if any vote roles exist using LinqToDB
        if (!await db.VoteRoles.AnyAsync(x => x.GuildId == guildId))
            return (false, Strings.NoVoteRolesSet(guildId));

        // Find the vote role using LinqToDB
        var voteRole = await db.VoteRoles
            .FirstOrDefaultAsync(x => x.RoleId == roleId && x.GuildId == guildId);

        if (voteRole is null)
            return (false, Strings.RoleNotVoteRole(guildId));

        // Delete vote role using LinqToDB
        await db.VoteRoles
            .Where(x => x.Id == voteRole.Id)
            .DeleteAsync();

        return (true, null);
    }

    /// <summary>
    ///     Updates the timer for automatic role removal for a vote role in the guild configuration.
    /// </summary>
    /// <param name="roleId">The ID of the role whose timer is to be updated.</param>
    /// <param name="seconds">The new duration in seconds after which the role should be automatically removed.</param>
    /// <param name="guildId">The guild identifier.</param>
    /// <returns>A tuple indicating success status and an optional error message.</returns>
    public async Task<(bool, string)> UpdateTimer(ulong guildId, ulong roleId, int seconds)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Find the vote role using LinqToDB
        var voteRole = await db.VoteRoles
            .FirstOrDefaultAsync(x => x.RoleId == roleId);

        if (voteRole is null)
            return (false, Strings.RoleNotAddedAsVoteRole(guildId));

        if (voteRole.Timer == seconds)
            return (false, Strings.VoteRoleTimerAlreadySet(voteRole.GuildId));

        voteRole.Timer = seconds;

        // Update vote role using LinqToDB
        await db.UpdateAsync(voteRole);

        return (true, null);
    }

    /// <summary>
    ///     Retrieves all vote roles configured for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which vote roles are requested.</param>
    /// <returns>A list of vote roles.</returns>
    public async Task<IList<VoteRole>> GetVoteRoles(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get vote roles using LinqToDB
        return await db.VoteRoles
            .Where(x => x.GuildId == guildId)
            .ToListAsync() ?? [];
    }

    /// <summary>
    ///     Gets the custom vote message configured for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which the vote message is requested.</param>
    /// <returns>The custom vote message, if any.</returns>
    public async Task<string> GetVoteMessage(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild config using LinqToDB
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        return gc?.VoteEmbed;
    }

    /// <summary>
    ///     Clears all vote roles configured for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild whose vote roles are to be cleared.</param>
    /// <returns>A tuple indicating success status and an optional error message.</returns>
    public async Task<(bool, string)> ClearVoteRoles(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if vote roles exist using LinqToDB
        if (!await db.VoteRoles.AnyAsync(x => x.GuildId == guildId))
            return (false, Strings.NoVoteRolesConfigured(guildId));

        // Delete all vote roles for guild using LinqToDB
        await db.VoteRoles
            .Where(x => x.GuildId == guildId)
            .DeleteAsync();

        return (true, null);
    }

    /// <summary>
    ///     Sets or updates the custom vote message for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which the vote message is to be set.</param>
    /// <param name="message">The new custom vote message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetVoteMessage(ulong guildId, string message)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild config using LinqToDB
        var config = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config == null)
        {
            // Create new guild config if it doesn't exist
            config = new GuildConfig
            {
                GuildId = guildId
            };
            config.VoteEmbed = message;
            await db.InsertAsync(config);
        }
        else
        {
            // Update existing guild config
            config.VoteEmbed = message;
            await db.UpdateAsync(config);
        }
    }

    /// <summary>
    ///     Sets or updates the password required for vote validation in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which the vote password is to be set.</param>
    /// <param name="password">The new password for vote validation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetVotePassword(ulong guildId, string password)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild config using LinqToDB
        var config = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config == null)
        {
            // Create new guild config if it doesn't exist
            config = new GuildConfig
            {
                GuildId = guildId
            };
            config.VotesPassword = password;
            await db.InsertAsync(config);
        }
        else
        {
            // Update existing guild config
            config.VotesPassword = password;
            await db.UpdateAsync(config);
        }
    }

    /// <summary>
    ///     Sets or updates the channel ID where vote acknowledgements should be sent in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which the vote channel is to be set.</param>
    /// <param name="channel">The ID of the channel for vote acknowledgements.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetVoteChannel(ulong guildId, ulong channel)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild config using LinqToDB
        var config = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config == null)
        {
            // Create new guild config if it doesn't exist
            config = new GuildConfig
            {
                GuildId = guildId, VotesChannel = channel
            };
            await db.InsertAsync(config);
        }
        else
        {
            // Update existing guild config
            config.VotesChannel = channel;
            await db.UpdateAsync(config);
        }
    }

    /// <summary>
    ///     Retrieves all votes cast by a user in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A list of votes.</returns>
    public async Task<List<DataModel.Vote>> GetVotes(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get votes using LinqToDB
        return await db.Votes
            .Where(x => x.GuildId == guildId && x.UserId == userId)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves all votes cast in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of votes.</returns>
    public async Task<List<DataModel.Vote>> GetVotes(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get votes using LinqToDB
        return await db.Votes
            .Where(x => x.GuildId == guildId)
            .ToListAsync();
    }

    /// <summary>
    ///     Generates an embed with vote statistics for a user in a guild, including total votes and votes this month.
    /// </summary>
    /// <param name="user">The user for whom vote stats are generated.</param>
    /// <param name="guild">The guild in which the votes were cast.</param>
    /// <returns>An embed builder populated with vote statistics.</returns>
    public async Task<EmbedBuilder> GetTotalVotes(IUser user, IGuild guild)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Count votes for this month using LinqToDB
        var thisMonth = await db.Votes
            .CountAsync(x => x.DateAdded.Value.Month == DateTime.UtcNow.Month &&
                             x.UserId == user.Id &&
                             x.GuildId == guild.Id);

        // Count total votes using LinqToDB
        var total = await db.Votes
            .CountAsync(x => x.GuildId == guild.Id && x.UserId == user.Id);

        if (total is 0)
            return new EmbedBuilder().WithErrorColor().WithDescription(Strings.YouDoNotHaveAnyVotes(guild.Id));

        return new EmbedBuilder()
            .WithOkColor(guild.Id)
            .WithTitle(Strings.VoteStatsFor(guild.Id, guild.Name))
            .AddField(Strings.VotesThisMonth(guild.Id), thisMonth)
            .AddField(Strings.TotalVotes(guild.Id), total)
            .WithThumbnailUrl(user.RealAvatarUrl().AbsoluteUri)
            .WithFooter(new EmbedFooterBuilder().WithIconUrl(user.RealAvatarUrl().AbsoluteUri)
                .WithText($"{user} | {user.Id}"));
    }
}

/// <summary>
///     Represents a custom structure for holding user vote information.
/// </summary>
public class CustomVoteThingy
{
    /// <summary>
    ///     Gets or sets the user associated with the vote count.
    /// </summary>
    public IUser User { get; set; }

    /// <summary>
    ///     Gets or sets the total vote count for the user.
    /// </summary>
    public int VoteCount { get; set; }
}