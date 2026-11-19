using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Games.Common;
using Poll = DataModel.Poll;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
/// Manages poll templates for quick poll creation.
/// </summary>
public class PollTemplateService : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<PollTemplateService> logger;

    /// <summary>
    /// Initializes a new instance of the PollTemplateService class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="logger">The logger instance.</param>
    public PollTemplateService(IDataConnectionFactory dbFactory, ILogger<PollTemplateService> logger)
    {
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <summary>
    /// Creates a new poll template.
    /// </summary>
    /// <param name="guildId">The Discord guild ID where the template will be available.</param>
    /// <param name="creatorId">The Discord user ID of the template creator.</param>
    /// <param name="name">The template name.</param>
    /// <param name="question">The template question.</param>
    /// <param name="options">The template options.</param>
    /// <param name="settings">The template settings.</param>
    /// <returns>The created template entity.</returns>
    public async Task<PollTemplate> CreateTemplateAsync(ulong guildId, ulong creatorId, string name,
        string question, List<PollOptionData> options, PollSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Template question cannot be empty", nameof(question));

        if (options?.Count < 1)
            throw new ArgumentException("Template must have at least one option", nameof(options));

        if (options.Count > 25)
            throw new ArgumentException("Template cannot have more than 25 options", nameof(options));

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Check if template name already exists in this guild
            var existingTemplate = await db.GetTable<PollTemplate>()
                .FirstOrDefaultAsync(t => t.GuildId == guildId &&
                                          t.Name.ToLower() == name.ToLower());

            if (existingTemplate != null)
                throw new InvalidOperationException($"A template with the name '{name}' already exists in this guild");

            var template = new PollTemplate
            {
                GuildId = guildId,
                CreatorId = creatorId,
                Name = name,
                Question = question,
                Options = JsonSerializer.Serialize(options),
                Settings = settings != null ? JsonSerializer.Serialize(settings) : null,
                CreatedAt = DateTime.UtcNow
            };

            var templateId = await db.InsertWithInt32IdentityAsync(template);
            template.Id = templateId;

            logger.LogInformation("Created poll template {TemplateId} '{Name}' in guild {GuildId} by user {CreatorId}",
                templateId, name, guildId, creatorId);

            return template;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create poll template '{Name}' in guild {GuildId}", name, guildId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all templates available in a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to search for templates.</param>
    /// <returns>A list of available templates.</returns>
    public async Task<List<PollTemplate>> GetTemplatesAsync(ulong guildId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            return await db.GetTable<PollTemplate>()
                .Where(t => t.GuildId == guildId)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get poll templates for guild {GuildId}", guildId);
            return new List<PollTemplate>();
        }
    }

    /// <summary>
    /// Retrieves a specific template by its ID.
    /// </summary>
    /// <param name="templateId">The ID of the template to retrieve.</param>
    /// <returns>The template if found, otherwise null.</returns>
    public async Task<PollTemplate?> GetTemplateAsync(int templateId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            return await db.GetTable<PollTemplate>()
                .FirstOrDefaultAsync(t => t.Id == templateId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get poll template {TemplateId}", templateId);
            return null;
        }
    }

    /// <summary>
    /// Retrieves a template by name within a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="name">The name of the template.</param>
    /// <returns>The template if found, otherwise null.</returns>
    public async Task<PollTemplate?> GetTemplateByNameAsync(ulong guildId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            return await db.GetTable<PollTemplate>()
                .FirstOrDefaultAsync(t => t.GuildId == guildId &&
                                          t.Name.ToLower() == name.ToLower());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get poll template '{Name}' in guild {GuildId}", name, guildId);
            return null;
        }
    }

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    /// <param name="templateId">The ID of the template to update.</param>
    /// <param name="name">The new template name.</param>
    /// <param name="question">The new template question.</param>
    /// <param name="options">The new template options.</param>
    /// <param name="settings">The new template settings.</param>
    /// <param name="updatedBy">The Discord user ID of who updated the template.</param>
    /// <returns>True if the template was successfully updated, otherwise false.</returns>
    public async Task<bool> UpdateTemplateAsync(int templateId, string? name, string? question,
        List<PollOptionData>? options, PollSettings? settings, ulong updatedBy)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var template = await db.GetTable<PollTemplate>()
                .FirstOrDefaultAsync(t => t.Id == templateId);

            if (template == null)
                return false;

            // Check if user has permission to update (creator or admin)
            if (template.CreatorId != updatedBy)
            {
                // Additional permission checking would be done here
                // For now, only allow the creator to update
                return false;
            }

            var hasChanges = false;

            if (!string.IsNullOrWhiteSpace(name) &&
                !string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                // Check if new name already exists
                var existingTemplate = await db.GetTable<PollTemplate>()
                    .FirstOrDefaultAsync(t => t.GuildId == template.GuildId &&
                                              t.Name.ToLower() == name.ToLower() &&
                                              t.Id != templateId);

                if (existingTemplate != null)
                    throw new InvalidOperationException(
                        $"A template with the name '{name}' already exists in this guild");

                template.Name = name;
                hasChanges = true;
            }

            if (!string.IsNullOrWhiteSpace(question) && !string.Equals(template.Question, question))
            {
                template.Question = question;
                hasChanges = true;
            }

            if (options?.Count > 0)
            {
                if (options.Count > 25)
                    throw new ArgumentException("Template cannot have more than 25 options", nameof(options));

                var newOptionsJson = JsonSerializer.Serialize(options);
                if (!string.Equals(template.Options, newOptionsJson))
                {
                    template.Options = newOptionsJson;
                    hasChanges = true;
                }
            }

            if (settings != null)
            {
                var newSettingsJson = JsonSerializer.Serialize(settings);
                if (!string.Equals(template.Settings, newSettingsJson))
                {
                    template.Settings = newSettingsJson;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await db.UpdateAsync(template);
                logger.LogInformation("Updated poll template {TemplateId} by user {UserId}", templateId, updatedBy);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update poll template {TemplateId}", templateId);
            return false;
        }
    }

    /// <summary>
    /// Deletes a poll template.
    /// </summary>
    /// <param name="templateId">The ID of the template to delete.</param>
    /// <param name="deletedBy">The Discord user ID of who deleted the template.</param>
    /// <returns>True if the template was successfully deleted, otherwise false.</returns>
    public async Task<bool> DeleteTemplateAsync(int templateId, ulong deletedBy)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var template = await db.GetTable<PollTemplate>()
                .FirstOrDefaultAsync(t => t.Id == templateId);

            if (template == null)
                return false;

            // Check if user has permission to delete (creator or admin)
            if (template.CreatorId != deletedBy)
            {
                // Additional permission checking would be done here
                // For now, only allow the creator to delete
                return false;
            }

            await db.GetTable<PollTemplate>()
                .Where(t => t.Id == templateId)
                .DeleteAsync();

            logger.LogInformation("Deleted poll template {TemplateId} '{Name}' by user {UserId}",
                templateId, template.Name, deletedBy);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete poll template {TemplateId}", templateId);
            return false;
        }
    }

    /// <summary>
    /// Creates a poll from a template.
    /// </summary>
    /// <param name="templateId">The ID of the template to use.</param>
    /// <param name="channelId">The Discord channel ID where the poll will be posted.</param>
    /// <param name="messageId">The Discord message ID of the poll message.</param>
    /// <param name="creatorId">The Discord user ID of the poll creator.</param>
    /// <param name="pollType">The type of poll to create.</param>
    /// <param name="customSettings">Optional custom settings to override template settings.</param>
    /// <returns>The created poll if successful, otherwise null.</returns>
    public async Task<Poll?> CreatePollFromTemplateAsync(int templateId, ulong channelId, ulong messageId,
        ulong creatorId, PollType pollType, PollSettings? customSettings = null)
    {
        try
        {
            var template = await GetTemplateAsync(templateId);
            if (template == null)
                return null;

            var options = JsonSerializer.Deserialize<List<PollOptionData>>(template.Options);
            if (options == null || options.Count == 0)
                return null;

            var settings = customSettings;
            if (settings == null && !string.IsNullOrEmpty(template.Settings))
            {
                settings = JsonSerializer.Deserialize<PollSettings>(template.Settings);
            }

            // This would typically call ModernPollService.CreatePollAsync
            // For now, we'll return null and let the caller handle the actual poll creation
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create poll from template {TemplateId}", templateId);
            return null;
        }
    }

    /// <summary>
    /// Gets template usage statistics for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <returns>Dictionary of template names and their usage counts.</returns>
    public async Task<Dictionary<string, int>> GetTemplateUsageStatsAsync(ulong guildId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // This would require tracking template usage in polls
            // For now, return empty dictionary
            var templates = await GetTemplatesAsync(guildId);
            return templates.ToDictionary(t => t.Name, _ => 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get template usage stats for guild {GuildId}", guildId);
            return new Dictionary<string, int>();
        }
    }
}