using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.PubSub;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Votes.Common;

namespace Mewdeko.Modules.Votes.Services;

/// <summary>
/// Manages voting functionality within the bot, handling vote configuration, processing, and reward distribution.
/// </summary>
public class VoteService : INService
{
    private readonly DbContextProvider dbProvider;
    private readonly DiscordShardedClient client;
    private readonly MuteService muteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoteService"/> class, setting up dependencies and subscribing to voting-related pub/sub events.
    /// </summary>
    /// <param name="pubSub">The pub/sub system for event handling.</param>
    /// <param name="db">The database service for data access.</param>
    /// <param name="client">The Discord client for interacting with the Discord API.</param>
    /// <param name="muteService">The service for managing mutes within the bot.</param>
    public VoteService(IPubSub pubSub, DbContextProvider dbProvider, DiscordShardedClient client,
        MuteService muteService)
    {
        this.dbProvider = dbProvider;
        this.client = client;
        this.muteService = muteService;
        var typedKey = new TypedKey<CompoundVoteModal>("uservoted");
        pubSub.Sub(typedKey, RunVoteStuff);
    }

    private async ValueTask RunVoteStuff(CompoundVoteModal voteModal)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var potentialVoteConfig =
            await dbContext.GuildConfigs.FirstOrDefaultAsyncEF(x => x.VotesPassword == voteModal.Password);
        if (potentialVoteConfig is null)
            return;
        var guild = client.GetGuild(potentialVoteConfig.GuildId);
        if (guild is null)
            return;
        var user = guild.GetUser(ulong.Parse(voteModal.VoteModel.User));
        var newVote = new Database.Models.Votes
        {
            UserId = user.Id, GuildId = guild.Id
        };
        await dbContext.Votes.AddAsync(newVote);
        await dbContext.SaveChangesAsync();
        if (string.IsNullOrEmpty(potentialVoteConfig.VoteEmbed))
        {
            if (potentialVoteConfig.VotesChannel is 0)
                return;

            if (guild.GetTextChannel(potentialVoteConfig.VotesChannel) is not ITextChannel channel)
                return;

            var votes = await dbContext.Votes.CountAsyncEF(x => x.UserId == user.Id && x.GuildId == guild.Id);
            var eb = new EmbedBuilder()
                .WithTitle($"Thanks for voting for {guild.Name}")
                .WithDescription($"You have voted a total of {votes} times!")
                .WithThumbnailUrl(user.RealAvatarUrl().AbsoluteUri)
                .WithOkColor();

            await channel.SendMessageAsync(user.Mention, embed: eb.Build());

            if (!await dbContext.VoteRoles.AnyAsyncEF(x => x.GuildId == guild.Id))
                return;
            var split = dbContext.VoteRoles.Where(x => x.GuildId == guild.Id);
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
            var votes = dbContext.Votes.Where(x => x.UserId == user.Id && x.GuildId == guild.Id);
            var rep = new ReplacementBuilder()
                .WithDefault(user, null, guild, client)
                .WithOverride("%votestotalcount%", () => votes.Count().ToString())
                .WithOverride("%votesmonthcount%",
                    () => votes.Count(x => x.DateAdded.Value.Month == DateTime.UtcNow.Month).ToString()).Build();

            if (SmartEmbed.TryParse(rep.Replace(potentialVoteConfig.VoteEmbed), guild.Id, out var embeds,
                    out var plainText, out var components))
            {
                await channel.SendMessageAsync(plainText, embeds: embeds, components: components.Build());
            }
            else
            {
                await channel.SendMessageAsync(rep.Replace(potentialVoteConfig.VoteEmbed).SanitizeMentions());
            }

            if (!await dbContext.VoteRoles.AnyAsyncEF(x => x.GuildId == guild.Id))
                return;
            var split = dbContext.VoteRoles.Where(x => x.GuildId == guild.Id);
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
    /// Adds a vote role to the guild configuration, setting a timer for automatic role removal if specified.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the role is to be added.</param>
    /// <param name="roleId">The ID of the role to be added as a vote role.</param>
    /// <param name="seconds">The duration in seconds after which the role should be automatically removed. Zero for indefinite.</param>
    /// <returns>A tuple indicating success status and an optional error message.</returns>
    public async Task<(bool, string)> AddVoteRole(ulong guildId, ulong roleId, int seconds = 0)
    {
        if (roleId == guildId)
            return (false, "Unable to add the everyone role you dumdum");
        await using var dbContext = await dbProvider.GetContextAsync();

        if (await dbContext.VoteRoles.CountAsyncEF(x => x.GuildId == guildId) >= 10)
            return (false, "Reached maximum of 10 VoteRoles");

        if (await dbContext.VoteRoles.Select(x => x.RoleId).ContainsAsyncEF(roleId))
            return (false, "Role is already set as a VoteRole.");

        var voteRole = new VoteRoles
        {
            RoleId = roleId, GuildId = guildId, Timer = seconds
        };

        await dbContext.VoteRoles.AddAsync(voteRole);
        await dbContext.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Removes a vote role from the guild configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild from which the role is to be removed.</param>
    /// <param name="roleId">The ID of the role to be removed from vote roles.</param>
    /// <returns>A tuple indicating success status and an optional error message.</returns>
    public async Task<(bool, string)> RemoveVoteRole(ulong guildId, ulong roleId)
    {
        if (roleId == guildId)
            return (false, "Unable to add the everyone role you dumdum");
        await using var dbContext = await dbProvider.GetContextAsync();

        if (!await dbContext.VoteRoles.AnyAsyncEF(x => x.GuildId == guildId))
            return (false, "You don't have any VoteRoles");
        var voteRole = await dbContext.VoteRoles.FirstOrDefaultAsyncEF(x => x.RoleId == roleId);
        if (voteRole is null)
            return (false, "Role is not a VoteRole.");

        dbContext.VoteRoles.Remove(voteRole);
        await dbContext.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Updates the timer for automatic role removal for a vote role in the guild configuration.
    /// </summary>
    /// <param name="roleId">The ID of the role whose timer is to be updated.</param>
    /// <param name="seconds">The new duration in seconds after which the role should be automatically removed.</param>
    /// <returns>A tuple indicating success status and an optional error message.</returns>
    public async Task<(bool, string)> UpdateTimer(ulong roleId, int seconds)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var voteRole = await dbContext.VoteRoles.FirstOrDefaultAsyncEF(x => x.RoleId == roleId);
        if (voteRole is null)
            return (false, "Role to update is not added as a VoteRole.");
        if (voteRole.Timer == seconds)
            return (false, "VoteRole timer is already set to that value.");
        voteRole.Timer = seconds;
        dbContext.VoteRoles.Update(voteRole);
        await dbContext.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Retrieves all vote roles configured for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which vote roles are requested.</param>
    /// <returns>A list of vote roles.</returns>
    public async Task<IList<VoteRoles>> GetVoteRoles(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.VoteRoles.Where(x => x.GuildId == guildId)?.ToListAsyncEF() ?? new List<VoteRoles>();
    }

    /// <summary>
    /// Gets the custom vote message configured for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which the vote message is requested.</param>
    /// <returns>The custom vote message, if any.</returns>
    public async Task<string> GetVoteMessage(ulong guildId)
    {

       await using var db = await dbProvider.GetContextAsync();
        var gc = await db.ForGuildId(guildId);
        return gc.VoteEmbed;
    }

    /// <summary>
    /// Clears all vote roles configured for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild whose vote roles are to be cleared.</param>
    /// <returns>A tuple indicating success status and an optional error message.</returns>
    public async Task<(bool, string)> ClearVoteRoles(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var voteRoles = dbContext.VoteRoles.Where(x => x.GuildId == guildId);
        if (!await voteRoles.AnyAsyncEF())
            return (false, "There are no VoteRoles set.");
        dbContext.VoteRoles.RemoveRange(voteRoles);
        await dbContext.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Sets or updates the custom vote message for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which the vote message is to be set.</param>
    /// <param name="message">The new custom vote message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetVoteMessage(ulong guildId, string message)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var config = await dbContext.ForGuildId(guildId, set => set);
        config.VoteEmbed = message;
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Sets or updates the password required for vote validation in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which the vote password is to be set.</param>
    /// <param name="password">The new password for vote validation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetVotePassword(ulong guildId, string password)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var config = await dbContext.ForGuildId(guildId, set => set);
        config.VotesPassword = password;
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Sets or updates the channel ID where vote acknowledgements should be sent in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which the vote channel is to be set.</param>
    /// <param name="channel">The ID of the channel for vote acknowledgements.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetVoteChannel(ulong guildId, ulong channel)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var config = await dbContext.ForGuildId(guildId, set => set);
        config.VotesChannel = channel;
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Retrieves all votes cast by a user in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A list of votes.</returns>
    public async Task<List<Database.Models.Votes>> GetVotes(ulong guildId, ulong userId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.Votes.Where(x => x.GuildId == guildId && x.UserId == userId).ToListAsyncEF();
    }

    /// <summary>
    /// Retrieves all votes cast in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of votes.</returns>
    public async Task<List<Database.Models.Votes>> GetVotes(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.Votes.Where(x => x.GuildId == guildId).ToListAsyncEF();
    }

    /// <summary>
    /// Generates an embed with vote statistics for a user in a guild, including total votes and votes this month.
    /// </summary>
    /// <param name="user">The user for whom vote stats are generated.</param>
    /// <param name="guild">The guild in which the votes were cast.</param>
    /// <returns>An embed builder populated with vote statistics.</returns>
    public async Task<EmbedBuilder> GetTotalVotes(IUser user, IGuild guild)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var thisMonth = await dbContext.Votes.CountAsyncEF(x =>
            x.DateAdded.Value.Month == DateTime.UtcNow.Month && x.UserId == user.Id && x.GuildId == guild.Id);
        var total = await dbContext.Votes.CountAsyncEF(x => x.GuildId == guild.Id && x.UserId == user.Id);
        if (total is 0)
            return new EmbedBuilder().WithErrorColor().WithDescription("You do not have any votes.");
        return new EmbedBuilder()
            .WithOkColor()
            .WithTitle($"Vote Stats for {guild.Name}")
            .AddField("Votes this month", thisMonth)
            .AddField("Total Votes", total)
            .WithThumbnailUrl(user.RealAvatarUrl().AbsoluteUri)
            .WithFooter(new EmbedFooterBuilder().WithIconUrl(user.RealAvatarUrl().AbsoluteUri)
                .WithText($"{user} | {user.Id}"));
    }
}

/// <summary>
/// Represents a custom structure for holding user vote information.
/// </summary>
public class CustomVoteThingy
{
    /// <summary>
    /// Gets or sets the user associated with the vote count.
    /// </summary>
    public IUser User { get; set; }

    /// <summary>
    /// Gets or sets the total vote count for the user.
    /// </summary>
    public int VoteCount { get; set; }
}