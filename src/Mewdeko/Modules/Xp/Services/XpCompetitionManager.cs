using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Manages XP competitions in guilds.
/// </summary>
public class XpCompetitionManager : INService, IDisposable
{
    // Active competitions
    private readonly ConcurrentDictionary<ulong, List<XpCompetition>> activeCompetitions = new();
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<XpCompetitionManager> logger;
    private readonly GeneratedBotStrings Strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpCompetitionManager" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="strings">The localized strings service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public XpCompetitionManager(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory, GeneratedBotStrings strings, ILogger<XpCompetitionManager> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        Strings = strings;
        this.logger = logger;
    }

    /// <summary>
    ///     Disposes of resources used by the competition manager.
    /// </summary>
    public void Dispose()
    {
        activeCompetitions.Clear();
    }

    /// <summary>
    ///     Loads active competitions for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadActiveCompetitionsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var now = DateTime.UtcNow;

        // Get competitions using LinqToDB
        var competitions = await db.XpCompetitions
            .Where(c => c.GuildId == guildId && c.StartTime <= now && c.EndTime >= now)
            .ToListAsync();

        if (competitions.Any())
        {
            activeCompetitions[guildId] = competitions;
        }
        else
        {
            activeCompetitions.TryRemove(guildId, out _);
        }
    }

    /// <summary>
    ///     Gets active competitions for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The list of active competitions.</returns>
    public async Task<List<XpCompetition>> GetActiveCompetitionsAsync(ulong guildId)
    {
        if (activeCompetitions.TryGetValue(guildId, out var competitions))
            return competitions;

        await LoadActiveCompetitionsAsync(guildId);

        return activeCompetitions.TryGetValue(guildId, out competitions)
            ? competitions
            : new List<XpCompetition>();
    }

    /// <summary>
    ///     Updates competition entries with new XP data.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="updates">The list of competition updates.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateCompetitionsAsync(MewdekoDb db, List<CompetitionUpdateItem> updates)
    {
        if (updates.Count == 0)
            return;

        try
        {
            // Group updates by competition
            var groupedUpdates = updates.GroupBy(u => u.CompetitionId).ToList();

            foreach (var group in groupedUpdates)
            {
                var competitionId = group.Key;

                // Get competition using LinqToDB
                var competition = await db.XpCompetitions
                    .FirstOrDefaultAsync(c => c.Id == competitionId);

                if (competition == null)
                    continue;

                foreach (var update in group)
                {
                    // Get or create entry using LinqToDB
                    var entry = await db.XpCompetitionEntries
                        .FirstOrDefaultAsync(e => e.CompetitionId == competitionId && e.UserId == update.UserId);

                    if (entry == null)
                    {
                        // Get user's current XP using LinqToDB
                        var userXp = await db.GuildUserXps
                            .FirstOrDefaultAsync(x => x.GuildId == competition.GuildId && x.UserId == update.UserId);

                        if (userXp == null)
                            continue;

                        // Create new entry
                        entry = new XpCompetitionEntry
                        {
                            CompetitionId = competitionId,
                            UserId = update.UserId,
                            StartingXp = userXp.TotalXp - update.XpGained,
                            CurrentXp = userXp.TotalXp
                        };

                        // Insert using LinqToDB
                        await db.InsertAsync(entry);
                    }
                    else
                    {
                        // Update existing entry
                        entry.CurrentXp += update.XpGained;

                        // Update using LinqToDB
                        await db.UpdateAsync(entry);
                    }

                    // Handle ReachLevel competitions
                    if ((XpCompetitionType)competition.Type == XpCompetitionType.ReachLevel &&
                        update.CurrentLevel >= competition.TargetLevel &&
                        entry.AchievedTargetAt == null)
                    {
                        entry.AchievedTargetAt = DateTime.UtcNow;

                        // Update using LinqToDB
                        await db.UpdateAsync(entry);

                        // Check if this is the first user to reach the target using LinqToDB
                        var isFirst = !(await db.XpCompetitionEntries
                            .AnyAsync(e => e.CompetitionId == competitionId &&
                                           e.UserId != update.UserId &&
                                           e.AchievedTargetAt != null));

                        if (isFirst && competition.AnnouncementChannelId.HasValue)
                        {
                            // Announce the achievement
                            var guild = client.GetGuild(competition.GuildId);
                            var channel = guild?.GetTextChannel(competition.AnnouncementChannelId.Value);
                            var user = guild?.GetUser(update.UserId);

                            if (channel != null && user != null)
                            {
                                await channel.SendMessageAsync(
                                    embed: new EmbedBuilder()
                                        .WithColor(Color.Gold)
                                        .WithTitle(Strings.CompetitionMilestone(guild.Id))
                                        .WithDescription(
                                            Strings.XpCompetitionWinner(guild.Id, user.Mention, competition.TargetLevel,
                                                competition.Name))
                                        .Build()
                                );
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating competition entries");
        }
    }

    /// <summary>
    ///     Creates a new XP competition.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The competition name.</param>
    /// <param name="type">The competition type.</param>
    /// <param name="startTime">The start time.</param>
    /// <param name="endTime">The end time.</param>
    /// <param name="targetLevel">The target level for ReachLevel competitions.</param>
    /// <param name="announcementChannelId">The channel ID for announcements.</param>
    /// <returns>The created competition.</returns>
    public async Task<XpCompetition> CreateCompetitionAsync(
        ulong guildId,
        string name,
        XpCompetitionType type,
        DateTime startTime,
        DateTime endTime,
        int targetLevel = 0,
        ulong? announcementChannelId = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var competition = new XpCompetition
        {
            GuildId = guildId,
            Name = name,
            Type = (int)type,
            StartTime = startTime,
            EndTime = endTime,
            TargetLevel = targetLevel,
            AnnouncementChannelId = announcementChannelId
        };

        competition.Id = await db.InsertWithInt32IdentityAsync(competition);

        // If competition is active, add to active competitions
        var now = DateTime.UtcNow;
        if (now >= startTime && now <= endTime)
        {
            await LoadActiveCompetitionsAsync(guildId);
        }

        return competition;
    }

    /// <summary>
    ///     Adds a reward to a competition.
    /// </summary>
    /// <param name="competitionId">The competition ID.</param>
    /// <param name="position">The position to reward.</param>
    /// <param name="roleId">The role ID to award.</param>
    /// <param name="xpAmount">The XP amount to award.</param>
    /// <param name="currencyAmount">The currency amount to award.</param>
    /// <param name="customReward">A custom reward description.</param>
    /// <returns>The created reward.</returns>
    public async Task<XpCompetitionReward> AddCompetitionRewardAsync(
        int competitionId,
        int position,
        ulong roleId = 0,
        int xpAmount = 0,
        long currencyAmount = 0,
        string customReward = "")
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var reward = new XpCompetitionReward
        {
            CompetitionId = competitionId,
            Position = position,
            RoleId = roleId,
            XpAmount = xpAmount,
            CurrencyAmount = currencyAmount,
            CustomReward = customReward
        };

        // Insert using LinqToDB
        await db.InsertAsync(reward);

        return reward;
    }

    /// <summary>
    ///     Starts a competition.
    /// </summary>
    /// <param name="competitionId">The competition ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartCompetitionAsync(int competitionId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get competition using LinqToDB
            var competition = await db.XpCompetitions
                .FirstOrDefaultAsync(c => c.Id == competitionId);

            if (competition == null)
                return;

            // Initialize entries for active users using LinqToDB
            var activeUsers = await db.GuildUserXps
                .Where(u => u.GuildId == competition.GuildId &&
                            u.LastActivity > DateTime.UtcNow.AddDays(-30))
                .ToListAsync();

            var entries = new List<XpCompetitionEntry>();

            foreach (var user in activeUsers)
            {
                entries.Add(new XpCompetitionEntry
                {
                    CompetitionId = competitionId,
                    UserId = user.UserId,
                    StartingXp = user.TotalXp,
                    CurrentXp = user.TotalXp
                });
            }

            // Insert entries in bulk using LinqToDB
            if (entries.Count > 0)
            {
                await db.BulkCopyAsync(entries);
            }

            // Announce competition start if channel is set
            if (competition.AnnouncementChannelId.HasValue)
            {
                var guild = client.GetGuild(competition.GuildId);
                var channel = guild?.GetTextChannel(competition.AnnouncementChannelId.Value);

                if (channel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithColor(Color.Blue)
                        .WithTitle(Strings.CompetitionStarted(guild.Id))
                        .WithDescription(Strings.CompetitionHasStarted(guild.Id, competition.Name))
                        .AddField("Type", competition.Type.ToString(), true)
                        .AddField("Ends", competition.EndTime.ToString("yyyy-MM-dd HH:mm UTC"), true);

                    if ((XpCompetitionType)competition.Type == XpCompetitionType.ReachLevel)
                    {
                        embed.AddField("Target Level", competition.TargetLevel.ToString(), true);
                    }

                    await channel.SendMessageAsync(embed: embed.Build());
                }
            }

            // Add to active competitions
            await LoadActiveCompetitionsAsync(competition.GuildId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting competition {CompetitionId}", competitionId);
        }
    }

    /// <summary>
    ///     Finalizes a competition and distributes rewards.
    /// </summary>
    /// <param name="competitionId">The competition ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task FinalizeCompetitionAsync(int competitionId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get competition using LinqToDB
            var competition = await db.XpCompetitions
                .FirstOrDefaultAsync(c => c.Id == competitionId);

            if (competition == null)
                return;

            // Get entries using LinqToDB
            var entries = await db.XpCompetitionEntries
                .Where(e => e.CompetitionId == competitionId)
                .ToListAsync();

            if (entries.Count == 0)
                return;

            List<XpCompetitionEntry> rankedEntries;

            switch ((XpCompetitionType)competition.Type)
            {
                case XpCompetitionType.MostGained:
                    rankedEntries = entries
                        .OrderByDescending(e => e.CurrentXp - e.StartingXp)
                        .ToList();
                    break;

                case XpCompetitionType.ReachLevel:
                    rankedEntries = entries
                        .Where(e => e.AchievedTargetAt != null)
                        .OrderBy(e => e.AchievedTargetAt)
                        .ToList();
                    break;

                case XpCompetitionType.HighestTotal:
                    rankedEntries = entries
                        .OrderByDescending(e => e.CurrentXp)
                        .ToList();
                    break;

                default:
                    rankedEntries = entries;
                    break;
            }

            // Assign placements
            for (var i = 0; i < rankedEntries.Count; i++)
            {
                rankedEntries[i].FinalPlacement = i + 1;

                // Update using LinqToDB
                await db.UpdateAsync(rankedEntries[i]);
            }

            // Get rewards using LinqToDB
            var rewards = await db.XpCompetitionRewards
                .Where(r => r.CompetitionId == competitionId)
                .ToListAsync();

            // Distribute rewards
            var guild = client.GetGuild(competition.GuildId);
            if (guild != null)
            {
                foreach (var entry in rankedEntries)
                {
                    var entryRewards = rewards.Where(r => r.Position == entry.FinalPlacement).ToList();

                    foreach (var reward in entryRewards)
                    {
                        var user = guild.GetUser(entry.UserId);
                        if (user == null)
                            continue;

                        // Role reward
                        if (reward.RoleId != 0)
                        {
                            var role = guild.GetRole(reward.RoleId);
                            if (role != null)
                            {
                                await user.AddRoleAsync(role);
                                await Task.Delay(100); // Avoid rate limiting
                            }
                        }

                        // XP reward
                        if (reward.XpAmount > 0)
                        {
                            // Get user XP using LinqToDB
                            var userXp = await db.GuildUserXps
                                .FirstOrDefaultAsync(x => x.GuildId == competition.GuildId && x.UserId == entry.UserId);

                            if (userXp != null)
                            {
                                userXp.BonusXp += reward.XpAmount;

                                // Update using LinqToDB
                                await db.UpdateAsync(userXp);
                            }
                        }

                        // Currency reward - handled by XpRewardManager
                    }
                }
            }

            // Announce competition end if channel is set
            if (competition.AnnouncementChannelId.HasValue)
            {
                var channel = guild?.GetTextChannel(competition.AnnouncementChannelId.Value);

                if (channel != null)
                {
                    var embed = new EmbedBuilder()
                        .WithColor(Color.Purple)
                        .WithTitle(Strings.CompetitionEnded(guild.Id))
                        .WithDescription(Strings.CompetitionHasEnded(guild.Id, competition.Name));

                    // Add top 3 winners
                    var winners = rankedEntries.Take(3).ToList();

                    for (var i = 0; i < winners.Count; i++)
                    {
                        var winner = winners[i];
                        var user = guild.GetUser(winner.UserId);
                        var username = user?.Username ?? winner.UserId.ToString();

                        var value = (XpCompetitionType)competition.Type switch
                        {
                            XpCompetitionType.MostGained => $"{winner.CurrentXp - winner.StartingXp:N0} XP gained",
                            XpCompetitionType.ReachLevel when winner.AchievedTargetAt != null =>
                                $"Reached level {competition.TargetLevel} at {winner.AchievedTargetAt?.ToString("yyyy-MM-dd HH:mm")}",
                            XpCompetitionType.HighestTotal => $"{winner.CurrentXp:N0} total XP",
                            _ => "Participated"
                        };

                        embed.AddField($"#{i + 1}: {username}", value);
                    }

                    await channel.SendMessageAsync(embed: embed.Build());
                }
            }

            // Remove from active competitions
            if (activeCompetitions.TryGetValue(competition.GuildId, out var competitions))
            {
                competitions.RemoveAll(c => c.Id == competitionId);

                if (competitions.Count == 0)
                {
                    activeCompetitions.TryRemove(competition.GuildId, out _);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finalizing competition {CompetitionId}", competitionId);
        }
    }
}