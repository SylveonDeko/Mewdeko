using System.IO;
using System.Text;
using System.Text.Json;
using DataModel;
using Mewdeko.Controllers.Common.Polls;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Embed = Discord.Embed;
using Poll = DataModel.Poll;

namespace Mewdeko.Controllers;

/// <summary>
/// API controller for managing poll operations through the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class PollController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly ILogger<PollController> logger;
    private readonly PollService pollService;
    private readonly PollSchedulerService schedulerService;
    private readonly PollTemplateService templateService;
    private readonly IDashboardAuditContext auditContext;

    /// <summary>
    /// Initializes a new instance of the PollController class.
    /// </summary>
    /// <param name="pollService">The poll service for managing polls.</param>
    /// <param name="templateService">The template service for managing templates.</param>
    /// <param name="schedulerService">The poll scheduler service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public PollController(PollService pollService, PollTemplateService templateService,
        PollSchedulerService schedulerService, DiscordShardedClient client, ILogger<PollController> logger,
        IDashboardAuditContext auditContext)
    {
        this.pollService = pollService;
        this.templateService = templateService;
        this.schedulerService = schedulerService;
        this.client = client;
        this.logger = logger;
        this.auditContext = auditContext;
    }

    /// <summary>
    /// Retrieves all polls for a specific guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to retrieve polls for.</param>
    /// <param name="includeInactive">Whether to include closed/expired polls.</param>
    /// <returns>A list of polls with their current status and statistics.</returns>
    [HttpGet]
    public async Task<IActionResult> GetPolls(ulong guildId, bool includeInactive = false)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var polls = includeInactive
                ? await GetAllPollsForGuild(guildId)
                : await pollService.GetActivePollsAsync(guildId);

            var pollResponses = new List<PollResponse>();

            foreach (var poll in polls)
            {
                var channel = guild.GetTextChannel(poll.ChannelId);
                var creator =
                    await client.GetUserAsync(poll.CreatorId, CacheMode.AllowDownload, RequestOptions.Default);
                var stats = await pollService.GetPollStatsAsync(poll.Id);

                var response = await MapToPollResponse(poll, channel?.Name, creator?.Username, stats);
                pollResponses.Add(response);
            }

            return Ok(pollResponses.OrderByDescending(p => p.CreatedAt));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get polls for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Retrieves a specific poll by its ID.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="pollId">The ID of the poll to retrieve.</param>
    /// <returns>The poll data with detailed statistics.</returns>
    [HttpGet("{pollId}")]
    public async Task<IActionResult> GetPoll(ulong guildId, int pollId)
    {
        try
        {
            var poll = await pollService.GetPollAsync(pollId);
            if (poll == null || poll.GuildId != guildId)
                return NotFound("Poll not found");

            var guild = client.GetGuild(guildId);
            var channel = guild?.GetTextChannel(poll.ChannelId);
            var creator = await client.GetUserAsync(poll.CreatorId, CacheMode.AllowDownload, RequestOptions.Default);
            var stats = await pollService.GetPollStatsAsync(poll.Id);

            var response = await MapToPollResponse(poll, channel?.Name, creator?.Username, stats);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get poll {PollId} for guild {GuildId}", pollId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Creates a new poll in the specified guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID where the poll will be created.</param>
    /// <param name="request">The poll creation request data.</param>
    /// <returns>The created poll information.</returns>
    [HttpPost]
    public async Task<IActionResult> CreatePoll(ulong guildId, [FromBody] CreatePollRequest request)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var channel = guild.GetTextChannel(request.ChannelId);
            if (channel == null)
                return BadRequest("Channel not found");

            // Create poll message first
            var embed = await BuildPollEmbed(request, guildId);
            var components = BuildPollComponents(0, request.Options, request.Type); // Temporary ID

            var message = await channel.SendMessageAsync(embed: embed, components: components);

            // Convert request to poll options
            var pollOptions = request.Options.Select((opt, _) => new PollOptionData
            {
                Text = opt.Text, Color = opt.Color, Emote = opt.Emote
            }).ToList();

            // Create poll settings
            var settings = new PollSettings
            {
                AllowMultipleVotes = request.AllowMultipleVotes,
                IsAnonymous = request.IsAnonymous,
                AllowVoteChanges = request.AllowVoteChanges,
                AllowedRoles = request.AllowedRoles,
                Color = request.Color,
                ShowResults = request.ShowResults,
                ShowProgressBars = request.ShowProgressBars
            };

            var poll = await pollService.CreatePollAsync(guildId, request.ChannelId, message.Id,
                request.UserId, request.Question, pollOptions, request.Type, settings);

            // Update message with correct poll ID
            var updatedEmbed = await BuildPollEmbed(request, guildId, poll.Id);
            var updatedComponents = BuildPollComponents(poll.Id, request.Options, request.Type);
            await message.ModifyAsync(msg =>
            {
                msg.Embed = updatedEmbed;
                msg.Components = updatedComponents;
            });

            var response = await MapToPollResponse(poll, channel.Name, "Dashboard User", null);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create poll in guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Updates an existing poll's configuration.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="pollId">The ID of the poll to update.</param>
    /// <param name="request">The poll update request data.</param>
    /// <returns>The updated poll information.</returns>
    [HttpPatch("{pollId}")]
    public async Task<IActionResult> UpdatePoll(ulong guildId, int pollId, [FromBody] UpdatePollRequest request)
    {
        try
        {
            var poll = await pollService.GetPollAsync(pollId);
            if (poll == null || poll.GuildId != guildId)
                return NotFound("Poll not found");

            // Check permissions - only creator or manage messages can update
            if (poll.CreatorId != request.UserId)
            {
                var guild = client.GetGuild(guildId);
                var user = guild?.GetUser(request.UserId);
                if (user == null || !user.GuildPermissions.ManageMessages)
                    return Forbid("You don't have permission to update this poll");
            }

            auditContext.RecordBefore(poll);

            // Update poll settings
            var updateSuccess = await pollService.UpdatePollAsync(poll.Id, request);
            if (!updateSuccess)
                return BadRequest("Failed to update poll");

            // Get updated poll data
            poll = await pollService.GetPollAsync(pollId);
            auditContext.RecordAfter(poll);
            var guild2 = client.GetGuild(guildId);
            var channel = guild2?.GetTextChannel(poll.ChannelId);
            var creator = await client.GetUserAsync(poll.CreatorId, CacheMode.AllowDownload, RequestOptions.Default);
            var stats = await pollService.GetPollStatsAsync(poll.Id);

            var response = await MapToPollResponse(poll, channel?.Name, creator?.Username, stats);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update poll {PollId} for guild {GuildId}", pollId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Closes a poll manually before its expiration.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="pollId">The ID of the poll to close.</param>
    /// <param name="request">The poll close request data.</param>
    /// <returns>The result of the close operation.</returns>
    [HttpPost("{pollId}/close")]
    public async Task<IActionResult> ClosePoll(ulong guildId, int pollId, [FromBody] ClosePollRequest request)
    {
        try
        {
            var poll = await pollService.GetPollAsync(pollId);
            if (poll == null || poll.GuildId != guildId)
                return NotFound("Poll not found");

            auditContext.RecordBefore(poll);
            var success = await pollService.ClosePollAsync(pollId, request.UserId);
            if (!success)
                return BadRequest("Failed to close poll");

            // Update poll message to show closed status
            await UpdatePollMessage(poll, "Poll Closed");

            return Ok(new
            {
                message = "Poll closed successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to close poll {PollId} for guild {GuildId}", pollId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Deletes a poll and all associated data.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="pollId">The ID of the poll to delete.</param>
    /// <param name="userId">The Discord user ID of the person deleting the poll.</param>
    /// <returns>The result of the delete operation.</returns>
    [HttpDelete("{pollId}/{userId}")]
    public async Task<IActionResult> DeletePoll(ulong guildId, int pollId, ulong userId)
    {
        try
        {
            var poll = await pollService.GetPollAsync(pollId);
            if (poll == null || poll.GuildId != guildId)
                return NotFound("Poll not found");

            auditContext.RecordBefore(poll);
            var success = await pollService.DeletePollAsync(pollId, userId);
            if (!success)
                return BadRequest("Failed to delete poll");

            // Try to delete the poll message
            try
            {
                var guild = client.GetGuild(guildId);
                var channel = guild?.GetTextChannel(poll.ChannelId);
                var message = await channel?.GetMessageAsync(poll.MessageId);
                if (message != null)
                    await message.DeleteAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete poll message for poll {PollId}", pollId);
            }

            return Ok(new
            {
                message = "Poll deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete poll {PollId} for guild {GuildId}", pollId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Exports poll results in the specified format.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="pollId">The ID of the poll to export.</param>
    /// <param name="format">The export format (csv, json).</param>
    /// <returns>The exported poll data.</returns>
    [HttpGet("{pollId}/export")]
    public async Task<IActionResult> ExportPoll(ulong guildId, int pollId, string format = "json")
    {
        try
        {
            var poll = await pollService.GetPollAsync(pollId);
            if (poll == null || poll.GuildId != guildId)
                return NotFound("Poll not found");

            var stats = await pollService.GetPollStatsAsync(pollId);
            if (stats == null)
                return NotFound("Poll statistics not found");

            switch (format.ToLower())
            {
                case "csv":
                    var csv = GenerateCsvExport(poll, stats);
                    return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"poll-{pollId}-export.csv");

                default:
                    var jsonData = new
                    {
                        Poll = poll, Statistics = stats, ExportedAt = DateTime.UtcNow
                    };
                    return Ok(jsonData);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export poll {PollId} for guild {GuildId}", pollId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Retrieves analytics data for all polls in a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="timeframe">The time period for analytics (day, week, month, year).</param>
    /// <returns>The analytics data for the guild's polls.</returns>
    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics(ulong guildId, string timeframe = "month")
    {
        try
        {
            var polls = await GetAllPollsForGuild(guildId);

            var cutoffDate = timeframe.ToLower() switch
            {
                "day" => DateTime.UtcNow.AddDays(-1),
                "week" => DateTime.UtcNow.AddDays(-7),
                "month" => DateTime.UtcNow.AddMonths(-1),
                "year" => DateTime.UtcNow.AddYears(-1),
                _ => DateTime.UtcNow.AddMonths(-1)
            };

            var recentPolls = polls.Where(p => p.CreatedAt >= cutoffDate).ToList();

            // Calculate comprehensive analytics
            var totalVotes = 0;
            var voteDistribution = new Dictionary<PollType, int>();
            var hourlyDistribution = new Dictionary<int, int>();
            var dailyEngagement = new Dictionary<DateTime, double>();
            var topCreators = new Dictionary<ulong, int>();

            foreach (var poll in recentPolls)
            {
                var stats = await pollService.GetPollStatsAsync(poll.Id);
                if (stats != null)
                {
                    totalVotes += stats.TotalVotes;

                    // Track engagement by day
                    var date = poll.CreatedAt.Date;
                    if (!dailyEngagement.ContainsKey(date))
                        dailyEngagement[date] = 0;
                    dailyEngagement[date] += stats.TotalVotes;
                }

                // Track poll type distribution
                var pollType = (PollType)poll.Type;
                voteDistribution.TryGetValue(pollType, out var count);
                voteDistribution[pollType] = count + 1;

                // Track creation time distribution
                var hour = poll.CreatedAt.Hour;
                hourlyDistribution.TryGetValue(hour, out var hourCount);
                hourlyDistribution[hour] = hourCount + 1;

                // Track top creators
                topCreators.TryGetValue(poll.CreatorId, out var creatorCount);
                topCreators[poll.CreatorId] = creatorCount + 1;
            }

            var averageVotes = recentPolls.Count > 0 ? (double)totalVotes / recentPolls.Count : 0;
            var mostPopularType = voteDistribution.OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault().Key;

            var analytics = new
            {
                TotalPolls = recentPolls.Count,
                ActivePolls = recentPolls.Count(p => p.IsActive),
                ClosedPolls = recentPolls.Count(p => !p.IsActive),
                TotalVotes = totalVotes,
                AverageVotesPerPoll = Math.Round(averageVotes, 2),
                MostPopularPollType = mostPopularType,
                PollTypeDistribution = voteDistribution,
                PollsCreatedByDay = recentPolls.GroupBy(p => p.CreatedAt.Date)
                    .ToDictionary(g => g.Key, g => g.Count()),
                HourlyCreationDistribution = hourlyDistribution,
                DailyEngagement = dailyEngagement,
                TopCreators = topCreators.OrderByDescending(kvp => kvp.Value)
                    .Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Timeframe = timeframe,
                AnalysisDate = DateTime.UtcNow
            };

            return Ok(analytics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get analytics for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #region Templates

    /// <summary>
    /// Retrieves all poll templates for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <returns>A list of available templates.</returns>
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(ulong guildId)
    {
        try
        {
            var templates = await templateService.GetTemplatesAsync(guildId);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get templates for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Creates a new poll template.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="request">The template creation request.</param>
    /// <returns>The created template.</returns>
    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate(ulong guildId, [FromBody] CreateTemplateRequest request)
    {
        try
        {
            var options = request.Options.Select(opt => new PollOptionData
            {
                Text = opt.Text, Color = opt.Color, Emote = opt.Emote
            }).ToList();

            var settings = new PollSettings
            {
                AllowMultipleVotes = request.AllowMultipleVotes,
                IsAnonymous = request.IsAnonymous,
                AllowVoteChanges = request.AllowVoteChanges,
                Color = request.Color,
                ShowResults = request.ShowResults
            };

            var template = await templateService.CreateTemplateAsync(guildId, request.UserId,
                request.Name, request.Question, options, settings);

            return Ok(template);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create template for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Deletes a poll template.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="templateId">The ID of the template to delete.</param>
    /// <param name="userId">The Discord user ID of the person deleting the template.</param>
    /// <returns>The result of the delete operation.</returns>
    [HttpDelete("templates/{templateId}/{userId}")]
    public async Task<IActionResult> DeleteTemplate(ulong guildId, int templateId, ulong userId)
    {
        try
        {
            var template = await templateService.GetTemplateAsync(templateId);
            if (template == null || template.GuildId != guildId)
                return NotFound("Template not found");

            var success = await templateService.DeleteTemplateAsync(templateId, userId);
            if (!success)
                return BadRequest("Failed to delete template");

            return Ok(new
            {
                message = "Template deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete template {TemplateId} for guild {GuildId}", templateId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Exports poll templates to a JSON file.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <returns>The exported templates as a JSON file.</returns>
    [HttpGet("templates/export")]
    public async Task<IActionResult> ExportTemplates(ulong guildId)
    {
        try
        {
            var templates = await templateService.GetTemplatesAsync(guildId);
            var exportData = new
            {
                GuildId = guildId, ExportedAt = DateTime.UtcNow, TemplateCount = templates.Count, Templates = templates
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            return File(Encoding.UTF8.GetBytes(json), "application/json",
                $"poll-templates-{guildId}-{DateTime.UtcNow:yyyyMMdd}.json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export templates for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Imports poll templates from a JSON file.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="userId">The Discord user ID of the person importing templates.</param>
    /// <param name="file">The JSON file containing templates to import.</param>
    /// <returns>The result of the import operation.</returns>
    [HttpPost("templates/import/{userId}")]
    public async Task<IActionResult> ImportTemplates(ulong guildId, ulong userId, IFormFile? file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided");

            if (!file.ContentType.Contains("json"))
                return BadRequest("File must be JSON format");

            if (file.Length > 5 * 1024 * 1024) // 5MB limit
                return BadRequest("File size too large (max 5MB)");

            await using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            var importData = JsonSerializer.Deserialize<ImportTemplateData>(content);
            if (importData?.Templates == null)
                return BadRequest("Invalid file format");

            var importResults = new List<object>();
            var successCount = 0;
            var errorCount = 0;

            foreach (var template in importData.Templates)
            {
                try
                {
                    // Convert template data to proper format
                    var options = JsonSerializer.Deserialize<List<PollOptionData>>(template.Options);
                    var settings = !string.IsNullOrEmpty(template.Settings)
                        ? JsonSerializer.Deserialize<PollSettings>(template.Settings)
                        : new PollSettings();

                    var newTemplate = await templateService.CreateTemplateAsync(
                        guildId, userId, $"{template.Name}_imported", template.Question, options, settings);

                    importResults.Add(new
                    {
                        OriginalName = template.Name, ImportedName = newTemplate.Name, Success = true
                    });
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to import template {TemplateName}", template.Name);
                    importResults.Add(new
                    {
                        OriginalName = template.Name, Error = ex.Message, Success = false
                    });
                    errorCount++;
                }
            }

            return Ok(new
            {
                TotalTemplates = importData.Templates.Count,
                SuccessfulImports = successCount,
                FailedImports = errorCount,
                Results = importResults
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to import templates for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets detailed analytics for a specific poll.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="pollId">The ID of the poll to analyze.</param>
    /// <returns>Detailed analytics for the poll.</returns>
    [HttpGet("{pollId}/analytics")]
    public async Task<IActionResult> GetPollAnalytics(ulong guildId, int pollId)
    {
        try
        {
            var poll = await pollService.GetPollAsync(pollId);
            if (poll == null || poll.GuildId != guildId)
                return NotFound("Poll not found");

            var stats = await pollService.GetPollStatsAsync(pollId);
            if (stats == null)
                return NotFound("Poll statistics not found");

            var analytics = new
            {
                PollInfo = new
                {
                    poll.Id,
                    poll.Question,
                    Type = (PollType)poll.Type,
                    poll.IsActive,
                    poll.CreatedAt,
                    poll.ExpiresAt,
                    poll.ClosedAt
                },
                VotingStats = new
                {
                    stats.TotalVotes,
                    stats.UniqueVoters,
                    stats.ParticipationRate,
                    stats.AverageVoteTime,
                    VotesByHour = GetVotesByHour(stats.VoteHistory),
                    VotesByDay = GetVotesByDay(stats.VoteHistory),
                    stats.VotesByRole
                },
                OptionAnalytics = poll.PollOptions.OrderBy(o => o.Index).Select(option =>
                {
                    var voteCount = stats.OptionVotes.GetValueOrDefault(option.Index, 0);
                    var percentage = stats.TotalVotes > 0 ? (double)voteCount / stats.TotalVotes * 100 : 0;

                    return new
                    {
                        option.Index,
                        option.Text,
                        option.Emote,
                        VoteCount = voteCount,
                        Percentage = Math.Round(percentage, 2),
                        Rank = GetOptionRank(option.Index, stats.OptionVotes)
                    };
                }).ToList(),
                Timeline = GetVotingTimeline(stats.VoteHistory),
                Predictions = new
                {
                    EstimatedFinalVotes = EstimateFinalVotes(poll, stats),
                    TrendingOption = GetTrendingOption(stats.VoteHistory, poll.PollOptions.ToList()),
                    VelocityScore = CalculateVotingVelocity(stats.VoteHistory)
                }
            };

            return Ok(analytics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get analytics for poll {PollId} in guild {GuildId}", pollId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets voting trends across all polls in a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="days">Number of days to analyze (default: 30).</param>
    /// <returns>Voting trends and patterns.</returns>
    [HttpGet("trends")]
    public async Task<IActionResult> GetVotingTrends(ulong guildId, int days = 30)
    {
        try
        {
            var polls = await GetAllPollsForGuild(guildId);
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var recentPolls = polls.Where(p => p.CreatedAt >= cutoffDate).ToList();

            var trends = new
            {
                TimeFrame = $"Last {days} days",
                Summary = new
                {
                    TotalPolls = recentPolls.Count,
                    TotalVotes = 0, // Will be calculated below
                    AverageVotesPerPoll = 0.0,
                    MostActiveDay = DateTime.MinValue,
                    PeakVotingHour = 0
                },
                PollTypePreferences = recentPolls.GroupBy(p => (PollType)p.Type)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                CreationPatterns = new
                {
                    ByHour = recentPolls.GroupBy(p => p.CreatedAt.Hour)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ByDayOfWeek = recentPolls.GroupBy(p => p.CreatedAt.DayOfWeek)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    ByDate = recentPolls.GroupBy(p => p.CreatedAt.Date)
                        .ToDictionary(g => g.Key, g => g.Count())
                },
                PerformanceMetrics = new
                {
                    AverageResponseTime = TimeSpan.Zero, // Will be calculated
                    CompletionRate = 0.0, // Polls that received votes vs total
                    EngagementScore = 0.0 // Average votes per poll relative to server size
                }
            };

            return Ok(trends);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get voting trends for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets comparative analytics between multiple polls.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="pollIds">Array of poll IDs to compare.</param>
    /// <returns>Comparative analytics data.</returns>
    [HttpPost("compare")]
    public async Task<IActionResult> ComparePoll(ulong guildId, [FromBody] int[]? pollIds)
    {
        try
        {
            if (pollIds == null || pollIds.Length < 2)
                return BadRequest("At least 2 poll IDs required for comparison");

            if (pollIds.Length > 10)
                return BadRequest("Maximum 10 polls can be compared at once");

            var comparisons = new List<object>();

            foreach (var pollId in pollIds)
            {
                var poll = await pollService.GetPollAsync(pollId);
                if (poll == null || poll.GuildId != guildId)
                    continue;

                var stats = await pollService.GetPollStatsAsync(pollId);
                if (stats == null)
                    continue;

                comparisons.Add(new
                {
                    PollId = poll.Id,
                    Question = poll.Question.Length > 50 ? poll.Question[..47] + "..." : poll.Question,
                    Type = (PollType)poll.Type,
                    stats.TotalVotes,
                    stats.UniqueVoters,
                    stats.ParticipationRate,
                    Duration = poll.ClosedAt.HasValue
                        ? poll.ClosedAt.Value - poll.CreatedAt
                        : DateTime.UtcNow - poll.CreatedAt,
                    VotesPerHour = CalculateVotesPerHour(poll, stats),
                    WinningOption = GetWinningOption(poll, stats),
                    CompetitivenessScore = CalculateCompetitivenessScore(stats)
                });
            }

            return Ok(new
            {
                ComparedPolls = comparisons.Count,
                Data = comparisons,
                Summary = new
                {
                    HighestEngagement = comparisons.Count > 0
                        ? comparisons.Cast<dynamic>().Max(c => c.TotalVotes)
                        : 0,
                    AverageVotes = comparisons.Count > 0
                        ? comparisons.Cast<dynamic>().Average(c => c.TotalVotes)
                        : 0,
                    FastestResponse = comparisons.Count > 0
                        ? comparisons.Cast<dynamic>().Max(c => c.VotesPerHour)
                        : 0
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compare polls for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Schedules a poll to be created at a future time.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="request">The scheduled poll creation request.</param>
    /// <returns>The result of the scheduling operation.</returns>
    [HttpPost("schedule")]
    public async Task<IActionResult> SchedulePoll(ulong guildId, [FromBody] SchedulePollRequest request)
    {
        try
        {
            if (request.ScheduledFor <= DateTime.UtcNow)
                return BadRequest("Scheduled time must be in the future");

            if (request.ScheduledFor > DateTime.UtcNow.AddDays(30))
                return BadRequest("Cannot schedule polls more than 30 days in advance");

            var guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var channel = guild.GetTextChannel(request.ChannelId);
            if (channel == null)
                return BadRequest("Channel not found");

            // Convert request options to poll option data
            var pollOptions = request.Options.Select(opt => new PollOptionData
            {
                Text = opt.Text, Color = opt.Color, Emote = opt.Emote
            }).ToList();

            // Create poll settings
            var settings = new PollSettings
            {
                AllowMultipleVotes = request.AllowMultipleVotes,
                IsAnonymous = request.IsAnonymous,
                AllowVoteChanges = request.AllowVoteChanges,
                AllowedRoles = request.AllowedRoles,
                Color = request.Color,
                ShowResults = request.ShowResults,
                ShowProgressBars = request.ShowProgressBars
            };

            // Schedule the poll
            var scheduledPoll = await schedulerService.SchedulePollAsync(
                guildId, request.ChannelId, request.UserId, request.Question,
                pollOptions, request.Type, settings, request.ScheduledFor, request.DurationMinutes);

            return Ok(new
            {
                Message = "Poll scheduled successfully",
                request.ScheduledFor,
                ScheduledId = scheduledPoll.Id,
                TimeUntilExecution = request.ScheduledFor - DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to schedule poll for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets all scheduled polls for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <returns>List of scheduled polls.</returns>
    [HttpGet("scheduled")]
    public async Task<IActionResult> GetScheduledPolls(ulong guildId)
    {
        try
        {
            var scheduledPolls = await schedulerService.GetScheduledPollsAsync(guildId);

            var pollResponses = scheduledPolls.Select(sp => new
            {
                sp.Id,
                sp.Question,
                sp.Type,
                sp.ChannelId,
                sp.CreatorId,
                sp.ScheduledFor,
                sp.DurationMinutes,
                sp.ScheduledAt,
                sp.IsExecuted,
                sp.ExecutedAt,
                sp.CreatedPollId,
                sp.IsCancelled,
                sp.CancelledAt,
                TimeUntilExecution = sp.IsExecuted || sp.IsCancelled ? TimeSpan.Zero :
                    sp.ScheduledFor > DateTime.UtcNow ? sp.ScheduledFor - DateTime.UtcNow : TimeSpan.Zero
            }).ToList();

            return Ok(new
            {
                GuildId = guildId, ScheduledPolls = pollResponses, pollResponses.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get scheduled polls for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Cancels a scheduled poll.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="scheduledId">The ID of the scheduled poll.</param>
    /// <param name="userId">The Discord user ID of the person canceling.</param>
    /// <returns>The result of the cancellation.</returns>
    [HttpDelete("scheduled/{scheduledId}/{userId}")]
    public async Task<IActionResult> CancelScheduledPoll(ulong guildId, int scheduledId, ulong userId)
    {
        try
        {
            var scheduledPoll = await schedulerService.GetScheduledPollAsync(scheduledId);
            if (scheduledPoll == null || scheduledPoll.GuildId != guildId)
                return NotFound("Scheduled poll not found");

            if (scheduledPoll.IsExecuted)
                return BadRequest("Cannot cancel an already executed poll");

            if (scheduledPoll.IsCancelled)
                return BadRequest("Poll is already cancelled");

            // Check permissions - only creator or manage messages can cancel
            if (scheduledPoll.CreatorId != userId)
            {
                var guild = client.GetGuild(guildId);
                var user = guild?.GetUser(userId);
                if (user == null || !user.GuildPermissions.ManageMessages)
                    return Forbid("You don't have permission to cancel this scheduled poll");
            }

            var success = await schedulerService.CancelScheduledPollAsync(scheduledId, userId);
            if (!success)
                return BadRequest("Failed to cancel scheduled poll");

            return Ok(new
            {
                message = "Scheduled poll canceled successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel scheduled poll {ScheduledId} for guild {GuildId}", scheduledId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Maps a poll entity to a poll response DTO.
    /// </summary>
    /// <param name="poll">The poll entity.</param>
    /// <param name="channelName">The name of the channel.</param>
    /// <param name="creatorName">The name of the creator.</param>
    /// <param name="stats">The poll statistics.</param>
    /// <returns>The mapped poll response.</returns>
    private static async Task<PollResponse> MapToPollResponse(Poll poll, string? channelName,
        string? creatorName, PollStats? stats)
    {
        await Task.CompletedTask;
        var options = poll.PollOptions.Select(opt => new PollOptionResponse
        {
            Id = opt.Id,
            Text = opt.Text,
            Index = opt.Index,
            Color = opt.Color,
            Emote = opt.Emote,
            VoteCount = stats?.OptionVotes.GetValueOrDefault(opt.Index, 0) ?? 0,
            VotePercentage = CalculatePercentage(stats?.OptionVotes.GetValueOrDefault(opt.Index, 0) ?? 0,
                stats?.TotalVotes ?? 0)
        }).OrderBy(o => o.Index).ToList();

        return new PollResponse
        {
            Id = poll.Id,
            GuildId = poll.GuildId,
            ChannelId = poll.ChannelId,
            ChannelName = channelName,
            MessageId = poll.MessageId,
            CreatorId = poll.CreatorId,
            CreatorName = creatorName,
            Question = poll.Question,
            Type = (PollType)poll.Type,
            Options = options,
            CreatedAt = poll.CreatedAt,
            ExpiresAt = poll.ExpiresAt,
            ClosedAt = poll.ClosedAt,
            IsActive = poll.IsActive,
            Stats = stats != null ? MapToPollStatsResponse(stats) : null
        };
    }

    /// <summary>
    /// Maps poll statistics to a response DTO.
    /// </summary>
    /// <param name="stats">The poll statistics.</param>
    /// <returns>The mapped statistics response.</returns>
    private static PollStatsResponse MapToPollStatsResponse(PollStats stats)
    {
        return new PollStatsResponse
        {
            TotalVotes = stats.TotalVotes,
            UniqueVoters = stats.UniqueVoters,
            OptionVotes = stats.OptionVotes,
            VoteHistory = stats.VoteHistory.Select(vh => new VoteHistoryResponse
            {
                UserId = vh.UserId,
                Username = vh.Username,
                OptionIndices = vh.OptionIndices,
                VotedAt = vh.VotedAt,
                IsAnonymous = vh.IsAnonymous
            }).ToList(),
            VotesByRole = stats.VotesByRole,
            AverageVoteTime = stats.AverageVoteTime,
            ParticipationRate = stats.ParticipationRate
        };
    }

    /// <summary>
    /// Calculates the percentage for vote counts.
    /// </summary>
    /// <param name="voteCount">The number of votes.</param>
    /// <param name="totalVotes">The total number of votes.</param>
    /// <returns>The percentage as a double.</returns>
    private static double CalculatePercentage(int voteCount, int totalVotes)
    {
        return totalVotes > 0 ? Math.Round((double)voteCount / totalVotes * 100, 2) : 0;
    }

    /// <summary>
    /// Gets all polls for a guild (including inactive ones).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A list of all polls for the guild.</returns>
    private async Task<List<Poll>> GetAllPollsForGuild(ulong guildId)
    {
        return await pollService.GetAllPollsAsync(guildId);
    }

    /// <summary>
    /// Builds the poll embed for display.
    /// </summary>
    /// <param name="request">The poll request.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="pollId">The poll ID (optional).</param>
    /// <returns>The built embed.</returns>
    private async Task<Embed> BuildPollEmbed(CreatePollRequest request, ulong guildId, int? pollId = null)
    {
        await Task.CompletedTask;

        var embed = new EmbedBuilder()
            .WithTitle($"📊 {request.Question}")
            .WithColor(Color.Blue)
            .WithTimestamp(DateTimeOffset.UtcNow);

        for (var i = 0; i < request.Options.Count; i++)
        {
            var option = request.Options[i];
            var optionText = $"{i + 1}. {option.Text}";
            if (!string.IsNullOrEmpty(option.Emote))
                optionText = $"{option.Emote} {optionText}";

            embed.AddField(optionText, "0 votes (0%)", true);
        }

        if (pollId.HasValue)
            embed.WithFooter($"Poll ID: {pollId.Value}");

        return embed.Build();
    }

    /// <summary>
    /// Builds the interactive components for the poll.
    /// </summary>
    /// <param name="pollId">The poll ID.</param>
    /// <param name="options">The poll options.</param>
    /// <param name="pollType">The poll type.</param>
    /// <returns>The built message component.</returns>
    private static MessageComponent BuildPollComponents(int pollId, List<PollOptionRequest> options, PollType pollType)
    {
        var builder = new ComponentBuilder();

        if (options.Count <= 5)
        {
            // Use buttons for 2-5 options
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var label = $"{i + 1}. {option.Text}";
                if (label.Length > 80) label = label[..77] + "...";

                var emote = !string.IsNullOrEmpty(option.Emote) ? Emote.Parse(option.Emote) : null;
                builder.WithButton(label, $"poll:vote:{pollId}:{i}", emote: emote);
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

                var emote = !string.IsNullOrEmpty(option.Emote) ? Emote.Parse(option.Emote) : null;
                selectMenuBuilder.AddOption(label, i.ToString(), emote: emote);
            }

            builder.WithSelectMenu(selectMenuBuilder);
        }

        // Add management buttons
        builder.WithButton("Close", $"poll:manage:{pollId}:close", ButtonStyle.Secondary)
            .WithButton("Stats", $"poll:manage:{pollId}:stats", ButtonStyle.Secondary)
            .WithButton("Delete", $"poll:manage:{pollId}:delete", ButtonStyle.Danger);

        return builder.Build();
    }

    /// <summary>
    /// Updates the poll message with new status.
    /// </summary>
    /// <param name="poll">The poll entity.</param>
    /// <param name="statusMessage">The status message to display.</param>
    private async Task UpdatePollMessage(Poll poll, string statusMessage)
    {
        try
        {
            var guild = client.GetGuild(poll.GuildId);
            var channel = guild?.GetTextChannel(poll.ChannelId);
            var message = await channel?.GetMessageAsync(poll.MessageId) as IUserMessage;

            if (message != null)
            {
                var embed = message.Embeds.FirstOrDefault()?.ToEmbedBuilder()
                    .WithColor(Color.Red)
                    .WithFooter($"{statusMessage} • Poll ID: {poll.Id}")
                    .Build();

                await message.ModifyAsync(msg =>
                {
                    msg.Embed = embed;
                    msg.Components = new ComponentBuilder().Build(); // Remove components
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update poll message for poll {PollId}", poll.Id);
        }
    }

    /// <summary>
    /// Generates a CSV export of poll data.
    /// </summary>
    /// <param name="poll">The poll entity.</param>
    /// <param name="stats">The poll statistics.</param>
    /// <returns>The CSV data as a string.</returns>
    private static string GenerateCsvExport(Poll poll, PollStats stats)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Poll Export");
        csv.AppendLine($"Question,{poll.Question}");
        csv.AppendLine($"Created At,{poll.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        csv.AppendLine($"Total Votes,{stats.TotalVotes}");
        csv.AppendLine($"Unique Voters,{stats.UniqueVoters}");
        csv.AppendLine();
        csv.AppendLine("Option,Votes,Percentage");

        foreach (var option in poll.PollOptions.OrderBy(o => o.Index))
        {
            var voteCount = stats.OptionVotes.GetValueOrDefault(option.Index, 0);
            var percentage = stats.TotalVotes > 0 ? (double)voteCount / stats.TotalVotes * 100 : 0;
            csv.AppendLine($"\"{option.Text}\",{voteCount},{percentage:F2}%");
        }

        return csv.ToString();
    }

    /// <summary>
    /// Groups votes by hour of day.
    /// </summary>
    /// <param name="voteHistory">The vote history.</param>
    /// <returns>Dictionary of hour to vote count.</returns>
    private static Dictionary<int, int> GetVotesByHour(List<VoteHistoryEntry> voteHistory)
    {
        return voteHistory.GroupBy(v => v.VotedAt.Hour)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Groups votes by day.
    /// </summary>
    /// <param name="voteHistory">The vote history.</param>
    /// <returns>Dictionary of date to vote count.</returns>
    private static Dictionary<DateTime, int> GetVotesByDay(List<VoteHistoryEntry> voteHistory)
    {
        return voteHistory.GroupBy(v => v.VotedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Gets the rank of an option based on vote counts.
    /// </summary>
    /// <param name="optionIndex">The option index.</param>
    /// <param name="optionVotes">All option votes.</param>
    /// <returns>The rank (1 = highest votes).</returns>
    private static int GetOptionRank(int optionIndex, Dictionary<int, int> optionVotes)
    {
        var voteCount = optionVotes.GetValueOrDefault(optionIndex, 0);
        return optionVotes.Values.Count(v => v > voteCount) + 1;
    }

    /// <summary>
    /// Creates a voting timeline showing vote progression.
    /// </summary>
    /// <param name="voteHistory">The vote history.</param>
    /// <returns>Timeline data points.</returns>
    private static List<object> GetVotingTimeline(List<VoteHistoryEntry> voteHistory)
    {
        return voteHistory.OrderBy(v => v.VotedAt)
            .Select((vote, index) => new
            {
                Timestamp = vote.VotedAt, CumulativeVotes = index + 1, vote.UserId
            })
            .Cast<object>()
            .ToList();
    }

    /// <summary>
    /// Estimates final vote count based on current trends.
    /// </summary>
    /// <param name="poll">The poll entity.</param>
    /// <param name="stats">Current statistics.</param>
    /// <returns>Estimated final vote count.</returns>
    private static int EstimateFinalVotes(Poll poll, PollStats stats)
    {
        if (!poll.IsActive || !poll.ExpiresAt.HasValue)
            return stats.TotalVotes;

        var elapsed = DateTime.UtcNow - poll.CreatedAt;
        var total = poll.ExpiresAt.Value - poll.CreatedAt;

        if (elapsed.TotalMinutes < 1 || total.TotalMinutes < 1)
            return stats.TotalVotes;

        var progressRatio = elapsed.TotalMinutes / total.TotalMinutes;
        return (int)Math.Ceiling(stats.TotalVotes / progressRatio);
    }

    /// <summary>
    /// Gets the currently trending option based on recent votes.
    /// </summary>
    /// <param name="voteHistory">The vote history.</param>
    /// <param name="options">Poll options.</param>
    /// <returns>The trending option index or null.</returns>
    private static int? GetTrendingOption(List<VoteHistoryEntry> voteHistory, List<PollOption> options)
    {
        var recentCutoff = DateTime.UtcNow.AddHours(-1);
        var recentVotes = voteHistory.Where(v => v.VotedAt >= recentCutoff).ToList();

        if (recentVotes.Count == 0)
            return null;

        var recentOptionCounts = new Dictionary<int, int>();
        foreach (var optionIndex in recentVotes.SelectMany(vote => vote.OptionIndices))
        {
            recentOptionCounts.TryGetValue(optionIndex, out var count);
            recentOptionCounts[optionIndex] = count + 1;
        }

        return recentOptionCounts.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;
    }

    /// <summary>
    /// Calculates voting velocity score.
    /// </summary>
    /// <param name="voteHistory">The vote history.</param>
    /// <returns>Velocity score (votes per hour).</returns>
    private static double CalculateVotingVelocity(List<VoteHistoryEntry> voteHistory)
    {
        if (voteHistory.Count == 0)
            return 0;

        var timeSpan = DateTime.UtcNow - voteHistory.Min(v => v.VotedAt);
        return timeSpan.TotalHours > 0 ? voteHistory.Count / timeSpan.TotalHours : 0;
    }

    /// <summary>
    /// Calculates votes per hour for a poll.
    /// </summary>
    /// <param name="poll">The poll entity.</param>
    /// <param name="stats">Poll statistics.</param>
    /// <returns>Votes per hour.</returns>
    private static double CalculateVotesPerHour(Poll poll, PollStats stats)
    {
        var duration = poll.ClosedAt.HasValue
            ? poll.ClosedAt.Value - poll.CreatedAt
            : DateTime.UtcNow - poll.CreatedAt;

        return duration.TotalHours > 0 ? stats.TotalVotes / duration.TotalHours : 0;
    }

    /// <summary>
    /// Gets the winning option for a poll.
    /// </summary>
    /// <param name="poll">The poll entity.</param>
    /// <param name="stats">Poll statistics.</param>
    /// <returns>Winning option text or null.</returns>
    private static string? GetWinningOption(Poll poll, PollStats stats)
    {
        var maxVotes = stats.OptionVotes.Values.DefaultIfEmpty(0).Max();
        if (maxVotes == 0)
            return null;

        var winningIndex = stats.OptionVotes.FirstOrDefault(kvp => kvp.Value == maxVotes).Key;
        return poll.PollOptions.FirstOrDefault(o => o.Index == winningIndex)?.Text;
    }

    /// <summary>
    /// Calculates competitiveness score (how close the results are).
    /// </summary>
    /// <param name="stats">Poll statistics.</param>
    /// <returns>Competitiveness score (0-100, higher = more competitive).</returns>
    private static double CalculateCompetitivenessScore(PollStats stats)
    {
        if (stats.TotalVotes == 0 || stats.OptionVotes.Count < 2)
            return 0;

        var votes = stats.OptionVotes.Values.OrderByDescending(v => v).ToList();
        var top = votes[0];
        var second = votes.Count > 1 ? votes[1] : 0;

        if (top == 0)
            return 0;

        return (double)second / top * 100;
    }

    #endregion
}

/// <summary>
/// Data structure for importing poll templates.
/// </summary>
public class ImportTemplateData
{
    /// <summary>
    /// Gets or sets the guild ID the templates were exported from.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the date the templates were exported.
    /// </summary>
    public DateTime ExportedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of templates in the export.
    /// </summary>
    public int TemplateCount { get; set; }

    /// <summary>
    /// Gets or sets the list of templates to import.
    /// </summary>
    public List<ImportTemplate> Templates { get; set; } = new();
}

/// <summary>
/// Data structure for a single template import.
/// </summary>
public class ImportTemplate
{
    /// <summary>
    /// Gets or sets the template ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template question.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template options as JSON.
    /// </summary>
    public string Options { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template settings as JSON.
    /// </summary>
    public string? Settings { get; set; }

    /// <summary>
    /// Gets or sets the creation date.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}