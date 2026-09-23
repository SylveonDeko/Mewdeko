using Mewdeko.Controllers.Common.WordOfTheDay;
using Mewdeko.Modules.WordOfTheDay.Common;
using Mewdeko.Modules.WordOfTheDay.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API controller for managing Word of the Day settings and custom words via the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class WordOfTheDayController : Controller
{
    private readonly IDashboardAuditContext auditContext;
    private readonly DiscordShardedClient client;
    private readonly ILogger<WordOfTheDayController> logger;
    private readonly WordOfTheDayService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WordOfTheDayController" /> class.
    /// </summary>
    /// <param name="service">The Word of the Day service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public WordOfTheDayController(
        WordOfTheDayService service,
        DiscordShardedClient client,
        ILogger<WordOfTheDayController> logger,
        IDashboardAuditContext auditContext)
    {
        this.service = service;
        this.client = client;
        this.logger = logger;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Gets the configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The configuration.</returns>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(ulong guildId)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        var config = await service.GetConfigAsync(guildId);
        var customCount = (await service.GetCustomWordsAsync(guildId)).Count;

        return Ok(new WordOfTheDayConfigResponse
        {
            ChannelId = config.ChannelId,
            Enabled = config.Enabled,
            PostHour = config.PostHour,
            Timezone = config.Timezone,
            PingRoleId = config.PingRoleId,
            MessageTemplate = config.MessageTemplate,
            Topic = config.Topic,
            PartOfSpeech = config.PartOfSpeech,
            Difficulty = config.Difficulty,
            SourceMode = config.SourceMode,
            CreateThread = config.CreateThread,
            ThreadName = config.ThreadName,
            ThreadAutoArchiveMinutes = config.ThreadAutoArchiveMinutes,
            LastPostedDate = config.LastPostedDate,
            CustomWordCount = customCount
        });
    }

    /// <summary>
    ///     Updates the configuration for a guild. Only supplied fields change.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The fields to update.</param>
    /// <returns>Success or a validation error.</returns>
    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig(ulong guildId, [FromBody] WordOfTheDayConfigRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        if (request.ChannelId is > 0 && guild.GetTextChannel(request.ChannelId.Value) is null)
            return BadRequest("Channel not found in guild.");

        if (request.PingRoleId is > 0 && guild.GetRole(request.PingRoleId.Value) is null)
            return BadRequest("Ping role not found in guild.");

        if (request.PostHour is < 0 or > 23)
            return BadRequest("PostHour must be between 0 and 23.");

        if (!string.IsNullOrWhiteSpace(request.Timezone) &&
            !WordOfTheDayService.TryGetTimeZone(request.Timezone, out _))
            return BadRequest("Invalid timezone.");

        if (request.PartOfSpeech is < 0 or > 4)
            return BadRequest("PartOfSpeech must be between 0 and 4.");

        if (request.Difficulty is < 0 or > 3)
            return BadRequest("Difficulty must be between 0 and 3.");

        if (request.SourceMode is < 0 or > 2)
            return BadRequest("SourceMode must be between 0 and 2.");

        if (request.ThreadAutoArchiveMinutes is int archive && archive is not (60 or 1440 or 4320 or 10080))
            return BadRequest("ThreadAutoArchiveMinutes must be 60, 1440, 4320, or 10080.");

        if (request.ThreadName is { Length: > 100 })
            return BadRequest("ThreadName must be 100 characters or fewer.");

        try
        {
            auditContext.RecordBefore(await service.GetConfigAsync(guildId));

            await service.UpdateConfigAsync(guildId, config =>
            {
                if (request.ChannelId.HasValue)
                    config.ChannelId = request.ChannelId.Value == 0 ? null : request.ChannelId.Value;
                if (request.Enabled.HasValue)
                    config.Enabled = request.Enabled.Value && config.ChannelId.HasValue;
                if (request.PostHour.HasValue)
                    config.PostHour = request.PostHour.Value;
                if (!string.IsNullOrWhiteSpace(request.Timezone))
                    config.Timezone = request.Timezone;
                if (request.PingRoleId.HasValue)
                    config.PingRoleId = request.PingRoleId.Value == 0 ? null : request.PingRoleId.Value;
                if (request.MessageTemplate is not null)
                    config.MessageTemplate = request.MessageTemplate.Length == 0 ? null : request.MessageTemplate;
                if (request.Topic is not null)
                    config.Topic = request.Topic.Length == 0 ? null : request.Topic;
                if (request.PartOfSpeech.HasValue)
                    config.PartOfSpeech = request.PartOfSpeech.Value;
                if (request.Difficulty.HasValue)
                    config.Difficulty = request.Difficulty.Value;
                if (request.SourceMode.HasValue)
                    config.SourceMode = request.SourceMode.Value;
                if (request.CreateThread.HasValue)
                    config.CreateThread = request.CreateThread.Value;
                if (request.ThreadName is not null)
                    config.ThreadName = request.ThreadName.Length == 0 ? null : request.ThreadName;
                if (request.ThreadAutoArchiveMinutes.HasValue)
                    config.ThreadAutoArchiveMinutes = request.ThreadAutoArchiveMinutes.Value;
            });

            auditContext.RecordAfter(await service.GetConfigAsync(guildId));
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update word of the day configuration for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to update configuration.");
        }
    }

    /// <summary>
    ///     Resets configuration, custom words, and history for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Success.</returns>
    [HttpPost("config/reset")]
    public async Task<IActionResult> ResetConfig(ulong guildId)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(await service.GetConfigAsync(guildId));
        await service.ResetAsync(guildId);
        auditContext.RecordAfter(await service.GetConfigAsync(guildId));
        return Ok();
    }

    /// <summary>
    ///     Posts a word immediately.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The posted word, or an error describing why nothing was posted.</returns>
    [HttpPost("post")]
    public async Task<IActionResult> PostNow(ulong guildId)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        var (entry, failure) = await service.PostNowAsync(guildId, true);
        if (entry is not null)
            return Ok(entry);

        return failure switch
        {
            "channel" => BadRequest("No valid channel is configured."),
            "send" => StatusCode(502, "Failed to send the message to Discord."),
            _ => StatusCode(503, "No word could be found with the current filters.")
        };
    }

    /// <summary>
    ///     Lists a guild's custom words.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The custom words.</returns>
    [HttpGet("words")]
    public async Task<IActionResult> GetWords(ulong guildId)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        var words = await service.GetCustomWordsAsync(guildId);
        return Ok(words.Select(w => new WordOfTheDayWordResponse
        {
            Id = w.Id,
            Word = w.Word,
            PartOfSpeech = w.PartOfSpeech,
            Definition = w.Definition,
            Example = w.Example,
            AddedBy = w.AddedBy,
            TimesUsed = w.TimesUsed,
            LastUsed = w.LastUsed,
            DateAdded = w.DateAdded
        }));
    }

    /// <summary>
    ///     Adds a custom word, looking up a definition when none is supplied.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The word to add.</param>
    /// <returns>The stored word.</returns>
    [HttpPost("words")]
    public async Task<IActionResult> AddWord(ulong guildId, [FromBody] WordOfTheDayAddWordRequest request)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        if (string.IsNullOrWhiteSpace(request.Word))
            return BadRequest("Word is required.");

        var (entry, exists) = await service.AddCustomWordAsync(guildId, request.AddedBy, request.Word,
            request.Definition);

        if (exists)
            return Conflict("Word already exists.");

        if (entry is null)
            return BadRequest("No definition could be found. Supply one explicitly.");

        return Ok(new WordOfTheDayWordResponse
        {
            Id = entry.Id,
            Word = entry.Word,
            PartOfSpeech = entry.PartOfSpeech,
            Definition = entry.Definition,
            Example = entry.Example,
            AddedBy = entry.AddedBy,
            TimesUsed = entry.TimesUsed,
            LastUsed = entry.LastUsed,
            DateAdded = entry.DateAdded
        });
    }

    /// <summary>
    ///     Removes a custom word.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="word">The word to remove.</param>
    /// <returns>Success or not found.</returns>
    [HttpDelete("words/{word}")]
    public async Task<IActionResult> RemoveWord(ulong guildId, string word)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        return await service.RemoveCustomWordAsync(guildId, word)
            ? Ok()
            : NotFound("Word not found.");
    }

    /// <summary>
    ///     Lists weekday and month rules.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The rules.</returns>
    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule(ulong guildId)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        var rules = await service.GetScheduleRulesAsync(guildId);
        return Ok(rules.Select(ToScheduleResponse));
    }

    /// <summary>
    ///     Creates or updates a weekday or month rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The rule.</param>
    /// <returns>The stored rule.</returns>
    [HttpPut("schedule")]
    public async Task<IActionResult> UpsertSchedule(ulong guildId, [FromBody] WordOfTheDayScheduleRequest request)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        if (request.RuleType is < 0 or > 1)
            return BadRequest("RuleType must be 0 (weekday) or 1 (month).");

        var maxKey = request.RuleType == 0 ? 6 : 12;
        var minKey = request.RuleType == 0 ? 0 : 1;
        if (request.RuleKey < minKey || request.RuleKey > maxKey)
            return BadRequest($"RuleKey must be between {minKey} and {maxKey}.");

        if (request.PartOfSpeech is < 0 or > 4)
            return BadRequest("PartOfSpeech must be between 0 and 4.");

        if (request.Difficulty is < 0 or > 3)
            return BadRequest("Difficulty must be between 0 and 3.");

        var rule = await service.UpsertScheduleRuleAsync(guildId, (ScheduleRuleType)request.RuleType,
            request.RuleKey, request.Topic,
            request.PartOfSpeech.HasValue ? (WordPartOfSpeech)request.PartOfSpeech.Value : null,
            request.Difficulty.HasValue ? (WordDifficulty)request.Difficulty.Value : null);

        return Ok(ToScheduleResponse(rule));
    }

    /// <summary>
    ///     Deletes a weekday or month rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ruleType">0 for weekday, 1 for month.</param>
    /// <param name="ruleKey">Day of week value, or month number.</param>
    /// <returns>Success or not found.</returns>
    [HttpDelete("schedule/{ruleType:int}/{ruleKey:int}")]
    public async Task<IActionResult> DeleteSchedule(ulong guildId, int ruleType, int ruleKey)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        if (ruleType is < 0 or > 1)
            return BadRequest("RuleType must be 0 (weekday) or 1 (month).");

        return await service.RemoveScheduleRuleAsync(guildId, (ScheduleRuleType)ruleType, ruleKey)
            ? Ok()
            : NotFound("Rule not found.");
    }

    private static WordOfTheDayScheduleResponse ToScheduleResponse(DataModel.WordOfTheDaySchedule rule)
    {
        return new WordOfTheDayScheduleResponse
        {
            Id = rule.Id,
            RuleType = rule.RuleType,
            RuleKey = rule.RuleKey,
            Topic = rule.Topic,
            PartOfSpeech = rule.PartOfSpeech,
            Difficulty = rule.Difficulty
        };
    }

    /// <summary>
    ///     Gets recently posted words.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="count">Maximum rows, capped at 100.</param>
    /// <returns>History rows, newest first.</returns>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(ulong guildId, [FromQuery] int count = 30)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        var history = await service.GetHistoryAsync(guildId, Math.Clamp(count, 1, 100));
        return Ok(history.Select(h => new WordOfTheDayHistoryResponse
        {
            Id = h.Id,
            Word = h.Word,
            PartOfSpeech = h.PartOfSpeech,
            Definition = h.Definition,
            Example = h.Example,
            Phonetic = h.Phonetic,
            PostedOn = h.PostedOn
        }));
    }
}
