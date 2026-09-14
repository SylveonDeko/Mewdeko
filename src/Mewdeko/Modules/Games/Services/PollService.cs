using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Strings;
using Poll = DataModel.Poll;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
/// Provides business logic for poll management operations.
/// </summary>
public class PollService : INService, IReadyExecutor
{
    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<PollService> logger;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    /// Initializes a new instance of the PollService class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="strings">The localized strings service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="collector">The analytics collector.</param>
    public PollService(IDataConnectionFactory dbFactory, ILogger<PollService> logger,
        GeneratedBotStrings strings, DiscordShardedClient client, IAnalyticsCollector collector)
    {
        this.collector = collector;
        this.dbFactory = dbFactory;
        this.logger = logger;
        this.strings = strings;
        this.client = client;
    }

    /// <summary>
    /// Gets the active polls by guild ID.
    /// </summary>
    public ConcurrentDictionary<ulong, List<Poll>> ActivePolls { get; set; } = new();

    /// <summary>
    /// Loads active polls from the database into memory on startup.
    /// </summary>
    public async Task OnReadyAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var activePolls = await db.Polls
                .LoadWithAsTable(p => p.PollOptions)
                .LoadWithAsTable(p => p.PollVotes)
                .Where(p => p.IsActive)
                .ToListAsync();

            var groupedPolls = activePolls.GroupBy(p => p.GuildId)
                .ToDictionary(g => g.Key, g => g.ToList());

            ActivePolls = new ConcurrentDictionary<ulong, List<Poll>>(groupedPolls);

            logger.LogInformation("Loaded {Count} active polls across {GuildCount} guilds",
                activePolls.Count, groupedPolls.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load active polls on startup");
        }
    }

    /// <summary>
    /// Creates a new poll in the specified guild and channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID where the poll will be created.</param>
    /// <param name="channelId">The Discord channel ID where the poll will be posted.</param>
    /// <param name="messageId">The Discord message ID of the poll message.</param>
    /// <param name="creatorId">The Discord user ID of the poll creator.</param>
    /// <param name="question">The poll question text.</param>
    /// <param name="options">The list of poll options.</param>
    /// <param name="type">The type of poll to create.</param>
    /// <param name="settings">The poll configuration settings.</param>
    /// <returns>The created poll entity.</returns>
    public async Task<Poll> CreatePollAsync(ulong guildId, ulong channelId, ulong messageId, ulong creatorId,
        string question, List<PollOptionData> options, PollType type, PollSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Poll question cannot be empty", nameof(question));

        if (options?.Count < 1)
            throw new ArgumentException("Poll must have at least one option", nameof(options));

        if (options.Count > 25)
            throw new ArgumentException("Poll cannot have more than 25 options", nameof(options));

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var expiresAt = settings != null ? CalculateExpirationTime(settings) : null;

            var poll = new Poll
            {
                GuildId = guildId,
                ChannelId = channelId,
                MessageId = messageId,
                CreatorId = creatorId,
                Question = question,
                Type = (int)type,
                Settings = settings != null ? JsonSerializer.Serialize(settings) : null,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsActive = true
            };

            var pollId = await db.InsertWithInt32IdentityAsync(poll);
            poll.Id = pollId;

            // Insert poll options individually
            for (var i = 0; i < options.Count; i++)
            {
                var pollOption = new PollOption
                {
                    PollId = pollId,
                    Text = options[i].Text,
                    Index = i,
                    Color = options[i].Color,
                    Emote = options[i].Emote
                };

                await db.InsertAsync(pollOption);
            }

            // Add to active polls cache
            ActivePolls.AddOrUpdate(guildId,
                [poll],
                (_, existing) =>
                {
                    existing.Add(poll);
                    return existing;
                });

            logger.LogInformation("Created poll {PollId} in guild {GuildId} by user {CreatorId}",
                pollId, guildId, creatorId);

            collector.Feature("poll", guildId);
            return poll;
        }
        catch (Exception ex)
        {
            collector.Feature("poll", guildId, false, ex.GetType().Name);
            logger.LogError(ex, "Failed to create poll in guild {GuildId}", guildId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all active polls for a specific guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to search for polls.</param>
    /// <returns>A list of active polls in the guild.</returns>
    public async Task<List<Poll>> GetActivePollsAsync(ulong guildId)
    {
        if (ActivePolls.TryGetValue(guildId, out var cachedPolls))
            return cachedPolls.Where(p => p.IsActive).ToList();

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var polls = await db.Polls
                .LoadWithAsTable(p => p.PollOptions)
                .Where(p => p.GuildId == guildId && p.IsActive)
                .ToListAsync();

            if (polls.Count > 0)
                ActivePolls.TryAdd(guildId, polls);

            return polls;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get active polls for guild {GuildId}", guildId);
            return [];
        }
    }

    /// <summary>
    /// Retrieves all polls for a specific guild (both active and inactive).
    /// </summary>
    /// <param name="guildId">The Discord guild ID to search for polls.</param>
    /// <returns>A list of all polls in the guild.</returns>
    public async Task<List<Poll>> GetAllPollsAsync(ulong guildId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var polls = await db.Polls
                .LoadWithAsTable(p => p.PollOptions)
                .LoadWithAsTable(p => p.PollVotes)
                .Where(p => p.GuildId == guildId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return polls;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all polls for guild {GuildId}", guildId);
            return [];
        }
    }

    /// <summary>
    /// Retrieves a specific poll by its ID.
    /// </summary>
    /// <param name="pollId">The unique poll identifier.</param>
    /// <returns>The poll entity if found, otherwise null.</returns>
    public async Task<Poll?> GetPollAsync(int pollId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            return await db.Polls
                .LoadWithAsTable(p => p.PollOptions)
                .LoadWithAsTable(p => p.PollVotes)
                .FirstOrDefaultAsync(p => p.Id == pollId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get poll {PollId}", pollId);
            return null;
        }
    }

    /// <summary>
    /// Processes a vote attempt for a poll.
    /// </summary>
    /// <param name="pollId">The ID of the poll being voted on.</param>
    /// <param name="userId">The Discord user ID of the voter.</param>
    /// <param name="optionIndices">The selected option indices.</param>
    /// <returns>A tuple indicating success and the vote result type.</returns>
    public async Task<(bool Success, VoteResult Result)> ProcessVoteAsync(int pollId, ulong userId, int[] optionIndices)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var poll = await GetPollAsync(pollId);
            if (poll == null)
                return (false, VoteResult.InvalidOption);

            if (!poll.IsActive || (poll.ExpiresAt.HasValue && poll.ExpiresAt.Value <= DateTime.UtcNow))
                return (false, VoteResult.PollClosed);

            PollSettings settings;
            try
            {
                settings = poll.Settings != null
                    ? JsonSerializer.Deserialize<PollSettings>(poll.Settings) ?? new PollSettings()
                    : new PollSettings();
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize poll settings for poll {PollId}, using defaults", pollId);
                settings = new PollSettings();
            }

            // Validate option indices
            var validIndices = optionIndices.Where(i => i >= 0 && i < poll.PollOptions.Count()).ToArray();
            if (validIndices.Length == 0)
                return (false, VoteResult.InvalidOption);

            // Check role restrictions
            if (settings.AllowedRoles?.Count > 0)
            {
                try
                {
                    var guild = client.GetGuild(poll.GuildId);
                    var user = guild?.GetUser(userId);
                    if (user == null ||
                        !settings.AllowedRoles.Any(roleId => user.Roles.Select(x => x.Id).Contains(roleId)))
                        return (false, VoteResult.NotAllowed);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to check role restrictions for poll {PollId} and user {UserId}",
                        pollId, userId);
                    return (false, VoteResult.NotAllowed);
                }
            }

            // Check existing vote
            var existingVote = await db.PollVotes
                .FirstOrDefaultAsync(v => v.PollId == pollId && v.UserId == userId);

            if (existingVote != null)
            {
                if (!settings.AllowVoteChanges)
                    return (false, VoteResult.AlreadyVoted);

                int[] existingIndices;
                try
                {
                    existingIndices = JsonSerializer.Deserialize<int[]>(existingVote.OptionIndices) ??
                                      [];
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex,
                        "Failed to deserialize existing vote indices for poll {PollId}, treating as empty", pollId);
                    existingIndices = [];
                }

                if (existingIndices.SequenceEqual(validIndices))
                {
                    // Remove vote (user clicked same option)
                    await db.PollVotes
                        .Where(v => v.Id == existingVote.Id)
                        .DeleteAsync();
                    return (true, VoteResult.Removed);
                }

                // Update existing vote
                existingVote.OptionIndices = JsonSerializer.Serialize(validIndices);
                existingVote.VotedAt = DateTime.UtcNow;
                await db.UpdateAsync(existingVote);
                return (true, VoteResult.Changed);
            }

            // Create new vote
            var newVote = new PollVote
            {
                PollId = pollId,
                UserId = userId,
                OptionIndices = JsonSerializer.Serialize(validIndices),
                VotedAt = DateTime.UtcNow,
                IsAnonymous = settings.IsAnonymous
            };

            await db.InsertAsync(newVote);
            collector.Feature("poll", poll.GuildId);
            return (true, VoteResult.Success);
        }
        catch (Exception ex)
        {
            collector.Feature("poll", null, false, ex.GetType().Name);
            logger.LogError(ex, "Failed to process vote for poll {PollId} by user {UserId}", pollId, userId);
            return (false, VoteResult.InvalidOption);
        }
    }

    /// <summary>
    /// Closes a poll manually before its expiration time.
    /// </summary>
    /// <param name="pollId">The ID of the poll to close.</param>
    /// <param name="closedBy">The Discord user ID of who closed the poll.</param>
    /// <returns>True if the poll was successfully closed, otherwise false.</returns>
    public async Task<bool> ClosePollAsync(int pollId, ulong closedBy)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var poll = await db.Polls
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll is not { IsActive: true })
                return false;

            poll.IsActive = false;
            poll.ClosedAt = DateTime.UtcNow;

            await db.UpdateAsync(poll);

            // Remove from active polls cache
            if (ActivePolls.TryGetValue(poll.GuildId, out var guildPolls))
            {
                var pollToRemove = guildPolls.FirstOrDefault(p => p.Id == pollId);
                if (pollToRemove != null)
                {
                    pollToRemove.IsActive = false;
                    pollToRemove.ClosedAt = DateTime.UtcNow;
                }
            }

            logger.LogInformation("Poll {PollId} closed by user {UserId}", pollId, closedBy);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to close poll {PollId}", pollId);
            return false;
        }
    }

    /// <summary>
    /// Retrieves voting statistics for a specific poll.
    /// </summary>
    /// <param name="pollId">The ID of the poll to get statistics for.</param>
    /// <returns>The poll statistics data.</returns>
    public async Task<PollStats?> GetPollStatsAsync(int pollId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var poll = await db.Polls
                .LoadWithAsTable(p => p.PollOptions)
                .LoadWithAsTable(p => p.PollVotes)
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll == null)
                return null;

            var votes = poll.PollVotes.ToList();
            var totalVotes = votes.Count;
            var uniqueVoters = votes.Select(v => v.UserId).Distinct().Count();

            var optionVotes = new Dictionary<int, int>();
            var voteHistory = new List<VoteHistoryEntry>();

            foreach (var vote in votes)
            {
                var indices = JsonSerializer.Deserialize<int[]>(vote.OptionIndices) ?? [];

                foreach (var index in indices)
                {
                    optionVotes[index] = optionVotes.GetValueOrDefault(index, 0) + 1;
                }

                voteHistory.Add(new VoteHistoryEntry
                {
                    UserId = vote.UserId,
                    OptionIndices = indices,
                    VotedAt = vote.VotedAt,
                    IsAnonymous = vote.IsAnonymous
                });
            }

            var averageVoteTime = votes.Count > 0
                ? TimeSpan.FromMilliseconds(votes.Average(v => (v.VotedAt - poll.CreatedAt).TotalMilliseconds))
                : TimeSpan.Zero;

            return new PollStats
            {
                TotalVotes = totalVotes,
                UniqueVoters = uniqueVoters,
                OptionVotes = optionVotes,
                VoteHistory = voteHistory.OrderBy(v => v.VotedAt).ToList(),
                AverageVoteTime = averageVoteTime,
                ParticipationRate = 0 // Would need guild member count to calculate this
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get stats for poll {PollId}", pollId);
            return null;
        }
    }

    /// <summary>
    /// Updates an existing poll's settings.
    /// </summary>
    /// <param name="pollId">The ID of the poll to update.</param>
    /// <param name="request">The update request with new settings.</param>
    /// <returns>True if the poll was successfully updated, otherwise false.</returns>
    public async Task<bool> UpdatePollAsync(int pollId, object request)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var poll = await db.Polls
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll is not { IsActive: true })
                return false;

            // Parse existing settings
            PollSettings settings;
            try
            {
                settings = poll.Settings != null
                    ? JsonSerializer.Deserialize<PollSettings>(poll.Settings) ?? new PollSettings()
                    : new PollSettings();
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize poll settings for poll {PollId}, using defaults", pollId);
                settings = new PollSettings();
            }

            // Update settings based on request (using reflection to avoid strong coupling)
            var requestType = request.GetType();

            var allowMultipleVotesProperty = requestType.GetProperty("AllowMultipleVotes");
            if (allowMultipleVotesProperty?.GetValue(request) is bool allowMultipleVotes)
                settings.AllowMultipleVotes = allowMultipleVotes;

            var isAnonymousProperty = requestType.GetProperty("IsAnonymous");
            if (isAnonymousProperty?.GetValue(request) is bool isAnonymous)
                settings.IsAnonymous = isAnonymous;

            var allowVoteChangesProperty = requestType.GetProperty("AllowVoteChanges");
            if (allowVoteChangesProperty?.GetValue(request) is bool allowVoteChanges)
                settings.AllowVoteChanges = allowVoteChanges;

            var showResultsProperty = requestType.GetProperty("ShowResults");
            if (showResultsProperty?.GetValue(request) is bool showResults)
                settings.ShowResults = showResults;

            var showProgressBarsProperty = requestType.GetProperty("ShowProgressBars");
            if (showProgressBarsProperty?.GetValue(request) is bool showProgressBars)
                settings.ShowProgressBars = showProgressBars;

            var allowedRolesProperty = requestType.GetProperty("AllowedRoles");
            if (allowedRolesProperty?.GetValue(request) is List<ulong> allowedRoles)
                settings.AllowedRoles = allowedRoles;

            var colorProperty = requestType.GetProperty("Color");
            if (colorProperty?.GetValue(request) is string color)
                settings.Color = color;

            // Update duration if provided
            var durationProperty = requestType.GetProperty("DurationMinutes");
            if (durationProperty?.GetValue(request) is int durationMinutes and > 0)
            {
                poll.ExpiresAt = DateTime.UtcNow.AddMinutes(durationMinutes);
            }

            // Update question if provided
            var questionProperty = requestType.GetProperty("Question");
            if (questionProperty?.GetValue(request) is string question && !string.IsNullOrWhiteSpace(question))
            {
                poll.Question = question;
            }

            // Serialize updated settings
            poll.Settings = JsonSerializer.Serialize(settings);

            await db.UpdateAsync(poll);

            // Update cache
            if (ActivePolls.TryGetValue(poll.GuildId, out var guildPolls))
            {
                var index = guildPolls.FindIndex(p => p.Id == pollId);
                if (index >= 0)
                    guildPolls[index] = poll;
            }

            logger.LogInformation("Poll {PollId} updated successfully", pollId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update poll {PollId}", pollId);
            return false;
        }
    }

    /// <summary>
    /// Deletes a poll and all associated data.
    /// </summary>
    /// <param name="pollId">The ID of the poll to delete.</param>
    /// <param name="deletedBy">The Discord user ID of who deleted the poll.</param>
    /// <returns>True if the poll was successfully deleted, otherwise false.</returns>
    public async Task<bool> DeletePollAsync(int pollId, ulong deletedBy)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var poll = await db.Polls
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll == null)
                return false;

            // Delete votes first (foreign key constraint)
            await db.PollVotes
                .Where(v => v.PollId == pollId)
                .DeleteAsync();

            // Delete options
            await db.PollOptions
                .Where(o => o.PollId == pollId)
                .DeleteAsync();

            // Delete poll
            await db.Polls
                .Where(p => p.Id == pollId)
                .DeleteAsync();

            // Remove from active polls cache
            if (ActivePolls.TryGetValue(poll.GuildId, out var guildPolls))
            {
                guildPolls.RemoveAll(p => p.Id == pollId);
            }

            logger.LogInformation("Poll {PollId} deleted by user {UserId}", pollId, deletedBy);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete poll {PollId}", pollId);
            return false;
        }
    }

    /// <summary>
    /// Updates the message ID for a poll.
    /// </summary>
    /// <param name="pollId">The poll ID.</param>
    /// <param name="messageId">The new message ID.</param>
    /// <returns>True if the update was successful.</returns>
    public async Task<bool> UpdatePollMessageIdAsync(int pollId, ulong messageId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var poll = await db.Polls.FirstOrDefaultAsync(p => p.Id == pollId);
            if (poll == null)
                return false;

            poll.MessageId = messageId;
            await db.UpdateAsync(poll);

            // Update cache
            if (ActivePolls.TryGetValue(poll.GuildId, out var guildPolls))
            {
                var index = guildPolls.FindIndex(p => p.Id == pollId);
                if (index >= 0)
                    guildPolls[index] = poll;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update message ID for poll {PollId}", pollId);
            return false;
        }
    }

    /// <summary>
    /// Sets the expiration time for a poll.
    /// </summary>
    /// <param name="pollId">The poll ID.</param>
    /// <param name="duration">The duration until expiration.</param>
    /// <returns>True if the expiration was set successfully.</returns>
    public async Task<bool> SetPollExpirationAsync(int pollId, TimeSpan duration)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var poll = await db.Polls.FirstOrDefaultAsync(p => p.Id == pollId);
            if (poll == null)
                return false;

            poll.ExpiresAt = DateTime.UtcNow.Add(duration);
            await db.UpdateAsync(poll);

            // Update cache
            if (ActivePolls.TryGetValue(poll.GuildId, out var guildPolls))
            {
                var index = guildPolls.FindIndex(p => p.Id == pollId);
                if (index >= 0)
                    guildPolls[index] = poll;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set expiration for poll {PollId}", pollId);
            return false;
        }
    }

    /// <summary>
    /// Calculates the expiration time for a poll based on settings.
    /// </summary>
    /// <param name="settings">The poll settings containing duration information.</param>
    /// <returns>The calculated expiration time, or null if no duration is set.</returns>
    private static DateTime? CalculateExpirationTime(PollSettings settings)
    {
        if (settings == null)
            return null;

        var baseTime = DateTime.UtcNow;
        var totalMinutes = 0;

        // Calculate total duration in minutes
        if (settings.DurationDays.HasValue)
            totalMinutes += settings.DurationDays.Value * 24 * 60;

        if (settings.DurationHours.HasValue)
            totalMinutes += settings.DurationHours.Value * 60;

        if (settings.DurationMinutes.HasValue)
            totalMinutes += settings.DurationMinutes.Value;

        // Return null if no duration is specified
        if (totalMinutes <= 0)
            return null;

        // Maximum duration limit: 30 days
        var maxMinutes = 30 * 24 * 60;
        if (totalMinutes > maxMinutes)
            totalMinutes = maxMinutes;

        return baseTime.AddMinutes(totalMinutes);
    }
}