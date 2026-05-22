using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Services.Strings;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
/// Service that manages scheduled poll creation and execution.
/// </summary>
public class PollSchedulerService : INService, IReadyExecutor, IDisposable
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<PollSchedulerService> logger;
    private readonly PollService pollService;
    private readonly Timer schedulerTimer;
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly GeneratedBotStrings strings;

    /// <summary>
    /// Initializes a new instance of the PollSchedulerService class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="pollService">The poll service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="strings">The strings service.</param>
    public PollSchedulerService(IDataConnectionFactory dbFactory, PollService pollService,
        DiscordShardedClient client, ILogger<PollSchedulerService> logger, GeneratedBotStrings strings)
    {
        this.dbFactory = dbFactory;
        this.pollService = pollService;
        this.client = client;
        this.logger = logger;
        this.strings = strings;

        // Check for scheduled polls every minute
        schedulerTimer = new Timer(ProcessScheduledPolls, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Starts the scheduler service when the bot is ready.
    /// </summary>
    public async Task OnReadyAsync()
    {
        logger.LogInformation("Poll scheduler service is ready");
        await Task.CompletedTask;
        // Perform an initial check for scheduled polls
        ProcessScheduledPolls(null);
    }

    /// <summary>
    /// Schedules a poll to be created at a specific time.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="channelId">The Discord channel ID.</param>
    /// <param name="creatorId">The Discord user ID of the creator.</param>
    /// <param name="question">The poll question.</param>
    /// <param name="options">The poll options.</param>
    /// <param name="pollType">The poll type.</param>
    /// <param name="settings">The poll settings.</param>
    /// <param name="scheduledFor">When to create the poll.</param>
    /// <param name="durationMinutes">Poll duration in minutes.</param>
    /// <returns>The scheduled poll entity.</returns>
    public async Task<ScheduledPoll> SchedulePollAsync(ulong guildId, ulong channelId, ulong creatorId,
        string question, List<PollOptionData> options, PollType pollType, PollSettings settings,
        DateTime scheduledFor, int? durationMinutes = null)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var scheduledPoll = new ScheduledPoll
            {
                GuildId = guildId,
                ChannelId = channelId,
                CreatorId = creatorId,
                Question = question,
                Options = JsonSerializer.Serialize(options),
                Type = (int)pollType,
                Settings = JsonSerializer.Serialize(settings),
                ScheduledFor = scheduledFor,
                DurationMinutes = durationMinutes,
                ScheduledAt = DateTime.UtcNow
            };

            scheduledPoll.Id = await db.InsertWithInt32IdentityAsync(scheduledPoll);

            logger.LogInformation("Scheduled poll {ScheduledPollId} for {ScheduledFor} in guild {GuildId}",
                scheduledPoll.Id, scheduledFor, guildId);

            return scheduledPoll;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to schedule poll for guild {GuildId}", guildId);
            throw;
        }
    }

    /// <summary>
    /// Gets all scheduled polls for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="includeCancelled">Whether to include cancelled polls.</param>
    /// <param name="includeExecuted">Whether to include executed polls.</param>
    /// <returns>List of scheduled polls.</returns>
    public async Task<List<ScheduledPoll>> GetScheduledPollsAsync(ulong guildId,
        bool includeCancelled = false, bool includeExecuted = false)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var query = db.ScheduledPolls
                .Where(sp => sp.GuildId == guildId);

            if (!includeCancelled)
                query = query.Where(sp => !sp.IsCancelled);

            if (!includeExecuted)
                query = query.Where(sp => !sp.IsExecuted);

            return await query.OrderBy(sp => sp.ScheduledFor).ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get scheduled polls for guild {GuildId}", guildId);
            return new List<ScheduledPoll>();
        }
    }

    /// <summary>
    /// Cancels a scheduled poll.
    /// </summary>
    /// <param name="scheduledPollId">The ID of the scheduled poll.</param>
    /// <param name="cancelledBy">The Discord user ID of who cancelled it.</param>
    /// <returns>True if the poll was successfully cancelled.</returns>
    public async Task<bool> CancelScheduledPollAsync(int scheduledPollId, ulong cancelledBy)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var scheduledPoll = await db.GetTable<ScheduledPoll>()
                .FirstOrDefaultAsync(sp => sp.Id == scheduledPollId);

            if (scheduledPoll == null || scheduledPoll.IsExecuted || scheduledPoll.IsCancelled)
                return false;

            scheduledPoll.IsCancelled = true;
            scheduledPoll.CancelledAt = DateTime.UtcNow;
            scheduledPoll.CancelledBy = cancelledBy;

            await db.UpdateAsync(scheduledPoll);

            logger.LogInformation("Cancelled scheduled poll {ScheduledPollId} by user {UserId}",
                scheduledPollId, cancelledBy);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel scheduled poll {ScheduledPollId}", scheduledPollId);
            return false;
        }
    }

    /// <summary>
    /// Gets a scheduled poll by ID.
    /// </summary>
    /// <param name="scheduledPollId">The scheduled poll ID.</param>
    /// <returns>The scheduled poll or null if not found.</returns>
    public async Task<ScheduledPoll?> GetScheduledPollAsync(int scheduledPollId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            return await db.GetTable<ScheduledPoll>()
                .FirstOrDefaultAsync(sp => sp.Id == scheduledPollId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get scheduled poll {ScheduledPollId}", scheduledPollId);
            return null;
        }
    }

    /// <summary>
    /// Timer callback that processes scheduled polls that are ready to be created.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private async void ProcessScheduledPolls(object? state)
    {
        if (!await semaphore.WaitAsync(100)) // Don't block if already processing
            return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var now = DateTime.UtcNow;
            var duePolls = await db.GetTable<ScheduledPoll>()
                .Where(sp => !sp.IsExecuted && !sp.IsCancelled && sp.ScheduledFor <= now)
                .ToListAsync();

            if (duePolls.Count == 0)
                return;

            logger.LogInformation("Found {Count} scheduled polls ready for execution", duePolls.Count);

            foreach (var scheduledPoll in duePolls)
            {
                try
                {
                    await ExecuteScheduledPoll(scheduledPoll);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to execute scheduled poll {ScheduledPollId}", scheduledPoll.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during scheduled poll processing");
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Executes a scheduled poll by creating the actual poll.
    /// </summary>
    /// <param name="scheduledPoll">The scheduled poll to execute.</param>
    private async Task ExecuteScheduledPoll(ScheduledPoll scheduledPoll)
    {
        try
        {
            // Deserialize the stored data
            var options = JsonSerializer.Deserialize<List<PollOptionData>>(scheduledPoll.Options);
            var settings = !string.IsNullOrEmpty(scheduledPoll.Settings)
                ? JsonSerializer.Deserialize<PollSettings>(scheduledPoll.Settings)
                : new PollSettings();

            if (options == null)
            {
                logger.LogWarning("Scheduled poll {ScheduledPollId} has invalid options data", scheduledPoll.Id);
                await MarkScheduledPollAsExecuted(scheduledPoll.Id, null);
                return;
            }

            // Verify the guild and channel still exist
            var guild = client.GetGuild(scheduledPoll.GuildId);
            if (guild == null)
            {
                logger.LogWarning("Guild {GuildId} not found for scheduled poll {ScheduledPollId}",
                    scheduledPoll.GuildId, scheduledPoll.Id);
                await MarkScheduledPollAsExecuted(scheduledPoll.Id, null);
                return;
            }

            var channel = guild.GetTextChannel(scheduledPoll.ChannelId);
            if (channel == null)
            {
                logger.LogWarning("Channel {ChannelId} not found for scheduled poll {ScheduledPollId}",
                    scheduledPoll.ChannelId, scheduledPoll.Id);
                await MarkScheduledPollAsExecuted(scheduledPoll.Id, null);
                return;
            }

            // Create the poll
            var poll = await pollService.CreatePollAsync(
                scheduledPoll.GuildId,
                scheduledPoll.ChannelId,
                0, // Message ID will be set later
                scheduledPoll.CreatorId,
                scheduledPoll.Question,
                options,
                (PollType)scheduledPoll.Type,
                settings);

            // Build and send the poll message
            var embed = await BuildScheduledPollEmbed(scheduledPoll, options, (PollType)scheduledPoll.Type, poll.Id);
            var components = BuildScheduledPollComponents(poll.Id, options, (PollType)scheduledPoll.Type);

            var message = await channel.SendMessageAsync(embed: embed, components: components);

            // Update the poll with the message ID
            await pollService.UpdatePollMessageIdAsync(poll.Id, message.Id);

            // Set expiration if specified
            if (scheduledPoll.DurationMinutes.HasValue)
            {
                await pollService.SetPollExpirationAsync(poll.Id,
                    TimeSpan.FromMinutes(scheduledPoll.DurationMinutes.Value));
            }

            // Mark the scheduled poll as executed
            await MarkScheduledPollAsExecuted(scheduledPoll.Id, poll.Id);

            logger.LogInformation("Successfully executed scheduled poll {ScheduledPollId}, created poll {PollId}",
                scheduledPoll.Id, poll.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute scheduled poll {ScheduledPollId}", scheduledPoll.Id);
            await MarkScheduledPollAsExecuted(scheduledPoll.Id, null);
        }
    }

    /// <summary>
    /// Marks a scheduled poll as executed.
    /// </summary>
    /// <param name="scheduledPollId">The scheduled poll ID.</param>
    /// <param name="createdPollId">The created poll ID (if successful).</param>
    private async Task MarkScheduledPollAsExecuted(int scheduledPollId, int? createdPollId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var scheduledPoll = await db.GetTable<ScheduledPoll>()
                .FirstOrDefaultAsync(sp => sp.Id == scheduledPollId);

            if (scheduledPoll != null)
            {
                scheduledPoll.IsExecuted = true;
                scheduledPoll.ExecutedAt = DateTime.UtcNow;
                scheduledPoll.CreatedPollId = createdPollId;

                await db.UpdateAsync(scheduledPoll);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark scheduled poll {ScheduledPollId} as executed", scheduledPollId);
        }
    }

    /// <summary>
    /// Builds the embed for a scheduled poll.
    /// </summary>
    /// <param name="scheduledPoll">The scheduled poll.</param>
    /// <param name="options">The poll options.</param>
    /// <param name="pollType">The poll type.</param>
    /// <param name="pollId">The created poll ID.</param>
    /// <returns>The built embed.</returns>
    private async Task<Embed> BuildScheduledPollEmbed(ScheduledPoll scheduledPoll,
        List<PollOptionData> options, PollType pollType, int pollId)
    {
        await Task.CompletedTask;

        var typeIcon = pollType switch
        {
            PollType.YesNo => "✅❌",
            PollType.SingleChoice => "📊",
            PollType.MultiChoice => "☑️",
            PollType.Anonymous => "🔒",
            PollType.RoleRestricted => "👥",
            _ => "📊"
        };

        var embed = new EmbedBuilder()
            .WithTitle(strings.PollTitleFormat(scheduledPoll.GuildId, typeIcon, scheduledPoll.Question))
            .WithColor(GetPollColor(pollType))
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter(strings.PollScheduledFooter(scheduledPoll.GuildId, pollId));

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var optionText = $"{i + 1}. {option.Text}";
            if (!string.IsNullOrEmpty(option.Emote))
                optionText = $"{option.Emote} {optionText}";

            embed.AddField(optionText, "0 votes (0%)", true);
        }

        return embed.Build();
    }

    /// <summary>
    /// Builds the components for a scheduled poll.
    /// </summary>
    /// <param name="pollId">The poll ID.</param>
    /// <param name="options">The poll options.</param>
    /// <param name="pollType">The poll type.</param>
    /// <returns>The built message component.</returns>
    private static MessageComponent BuildScheduledPollComponents(int pollId,
        List<PollOptionData> options, PollType pollType)
    {
        var builder = new ComponentBuilder();

        if (options.Count <= 5)
        {
            // Use buttons for 2-5 options
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var label = $"{i + 1}";
                if (option.Text.Length <= 12)
                    label = $"{i + 1}. {option.Text}";

                if (label.Length > 80) label = label[..77] + "...";

                IEmote? emote = null;
                if (!string.IsNullOrEmpty(option.Emote))
                {
                    try
                    {
                        emote = Emote.Parse(option.Emote);
                    }
                    catch
                    {
                        emote = new Emoji(option.Emote);
                    }
                }

                var style = GetButtonStyle(i);
                builder.WithButton(label, $"poll:vote:{pollId}:{i}", style, emote);
            }
        }
        else
        {
            // Use select menu for 6+ options
            var selectMenuBuilder = new SelectMenuBuilder()
                .WithCustomId($"poll:select:{pollId}")
                .WithPlaceholder("Choose your option(s)...")
                .WithMinValues(1)
                .WithMaxValues(pollType == PollType.MultiChoice ? Math.Min(options.Count, 25) : 1);

            for (var i = 0; i < Math.Min(options.Count, 25); i++)
            {
                var option = options[i];
                var label = $"{i + 1}. {option.Text}";
                if (label.Length > 100) label = label[..97] + "...";

                IEmote? emote = null;
                if (!string.IsNullOrEmpty(option.Emote))
                {
                    try
                    {
                        emote = Emote.Parse(option.Emote);
                    }
                    catch
                    {
                        emote = new Emoji(option.Emote);
                    }
                }

                selectMenuBuilder.AddOption(label, i.ToString(), emote: emote);
            }

            builder.WithSelectMenu(selectMenuBuilder);
        }

        // Add management buttons on second row
        builder.WithButton("📊 Stats", $"poll:manage:{pollId}:stats", ButtonStyle.Secondary)
            .WithButton("🔒 Close", $"poll:manage:{pollId}:close", ButtonStyle.Secondary)
            .WithButton("🗑️ Delete", $"poll:manage:{pollId}:delete", ButtonStyle.Danger);

        return builder.Build();
    }

    /// <summary>
    /// Gets the color for a poll type.
    /// </summary>
    /// <param name="pollType">The poll type.</param>
    /// <returns>The color for the poll type.</returns>
    private static Color GetPollColor(PollType pollType)
    {
        return pollType switch
        {
            PollType.YesNo => Color.Green,
            PollType.SingleChoice => Color.Blue,
            PollType.MultiChoice => Color.Purple,
            PollType.Anonymous => Color.DarkGrey,
            PollType.RoleRestricted => Color.Orange,
            _ => Color.Blue
        };
    }

    /// <summary>
    /// Gets the button style based on option index.
    /// </summary>
    /// <param name="index">The option index.</param>
    /// <returns>The button style.</returns>
    private static ButtonStyle GetButtonStyle(int index)
    {
        return index switch
        {
            0 => ButtonStyle.Primary,
            1 => ButtonStyle.Secondary,
            2 => ButtonStyle.Success,
            3 => ButtonStyle.Danger,
            _ => ButtonStyle.Secondary
        };
    }

    /// <summary>
    /// Disposes the timer when the service is disposed.
    /// </summary>
    public void Dispose()
    {
        schedulerTimer.Dispose();
        semaphore.Dispose();
    }
}