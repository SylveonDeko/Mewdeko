using LinqToDB;
using LinqToDB.Async;
using Embed = DataModel.Embed;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Provides services for managing user and guild embed templates.
/// </summary>
public class EmbedService : INService
{
    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EmbedService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database factory.</param>
    public EmbedService(IDataConnectionFactory dbFactory)
    {
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     Retrieves an embed template by name for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="embedName">The name of the embed template.</param>
    /// <returns>A task that represents the asynchronous operation, containing the embed template if found.</returns>
    public async Task<Embed?> GetUserEmbedAsync(ulong userId, string embedName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.Embeds
            .FirstOrDefaultAsync(e => e.UserId == userId &&
                                      e.EmbedName == embedName &&
                                      e.GuildId == null);
    }

    /// <summary>
    ///     Retrieves an embed template by name for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="embedName">The name of the embed template.</param>
    /// <returns>A task that represents the asynchronous operation, containing the embed template if found.</returns>
    public async Task<Embed?> GetGuildEmbedAsync(ulong guildId, string embedName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.Embeds
            .FirstOrDefaultAsync(e => e.GuildId == guildId &&
                                      e.EmbedName == embedName &&
                                      e.IsGuildShared == true);
    }

    /// <summary>
    ///     Retrieves an embed template by name, checking user embeds first, then guild embeds.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <param name="embedName">The name of the embed template.</param>
    /// <returns>A task that represents the asynchronous operation, containing the embed template if found.</returns>
    public async Task<Embed?> GetEmbedTemplateAsync(ulong userId, ulong? guildId, string embedName)
    {
        // Check user embeds first
        var userEmbed = await GetUserEmbedAsync(userId, embedName);
        if (userEmbed != null)
            return userEmbed;

        // If not found and in guild context, check guild embeds
        if (!guildId.HasValue) return null;
        var guildEmbed = await GetGuildEmbedAsync(guildId.Value, embedName);
        return guildEmbed;
    }

    /// <summary>
    ///     Retrieves all embed templates for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of user embed templates.</returns>
    public async Task<List<Embed>> GetUserEmbedsAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.Embeds
            .Where(e => e.UserId == userId && e.GuildId == null)
            .OrderBy(e => e.EmbedName)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves all embed templates for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of guild embed templates.</returns>
    public async Task<List<Embed>> GetGuildEmbedsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.Embeds
            .Where(e => e.GuildId == guildId && e.IsGuildShared == true)
            .OrderBy(e => e.EmbedName)
            .ToListAsync();
    }

    /// <summary>
    ///     Creates a new user embed template.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="embedName">The name of the embed template.</param>
    /// <param name="jsonCode">The JSON representation of the embed.</param>
    /// <returns>A task that represents the asynchronous operation, containing the created embed template.</returns>
    public async Task<Embed> CreateUserEmbedAsync(ulong userId, string embedName, string jsonCode)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var embed = new Embed
        {
            UserId = userId,
            EmbedName = embedName,
            JsonCode = jsonCode,
            DateAdded = DateTime.UtcNow,
            GuildId = null,
            IsGuildShared = false
        };

        await db.InsertAsync(embed);
        return embed;
    }

    /// <summary>
    ///     Creates a new guild embed template.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user creating the template.</param>
    /// <param name="embedName">The name of the embed template.</param>
    /// <param name="jsonCode">The JSON representation of the embed.</param>
    /// <returns>A task that represents the asynchronous operation, containing the created embed template.</returns>
    public async Task<Embed> CreateGuildEmbedAsync(ulong guildId, ulong userId, string embedName, string jsonCode)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var embed = new Embed
        {
            UserId = userId,
            EmbedName = embedName,
            JsonCode = jsonCode,
            DateAdded = DateTime.UtcNow,
            GuildId = guildId,
            IsGuildShared = true
        };

        await db.InsertAsync(embed);
        return embed;
    }

    /// <summary>
    ///     Deletes a user embed template.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="embedName">The name of the embed template to delete.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating success.</returns>
    public async Task<bool> DeleteUserEmbedAsync(ulong userId, string embedName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var embed = await db.Embeds
            .FirstOrDefaultAsync(e => e.UserId == userId &&
                                      e.EmbedName == embedName &&
                                      e.GuildId == null);

        if (embed == null)
            return false;

        await db.DeleteAsync(embed);
        return true;
    }

    /// <summary>
    ///     Deletes a guild embed template.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="embedName">The name of the embed template to delete.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating success.</returns>
    public async Task<bool> DeleteGuildEmbedAsync(ulong guildId, string embedName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var embed = await db.Embeds
            .FirstOrDefaultAsync(e => e.GuildId == guildId &&
                                      e.EmbedName == embedName &&
                                      e.IsGuildShared == true);

        if (embed == null)
            return false;

        await db.DeleteAsync(embed);
        return true;
    }

    /// <summary>
    ///     Checks if a user embed template with the specified name exists.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="embedName">The name of the embed template.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating if the template exists.</returns>
    public async Task<bool> UserEmbedExistsAsync(ulong userId, string embedName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.Embeds
            .AnyAsync(e => e.UserId == userId &&
                           e.EmbedName == embedName &&
                           e.GuildId == null);
    }

    /// <summary>
    ///     Checks if a guild embed template with the specified name exists.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="embedName">The name of the embed template.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating if the template exists.</returns>
    public async Task<bool> GuildEmbedExistsAsync(ulong guildId, string embedName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.Embeds
            .AnyAsync(e => e.GuildId == guildId &&
                           e.EmbedName == embedName &&
                           e.IsGuildShared == true);
    }

    /// <summary>
    ///     Retrieves an embed template by its database ID.
    /// </summary>
    /// <param name="id">The ID of the embed template.</param>
    /// <returns>A task that represents the asynchronous operation, containing the embed template if found.</returns>
    public async Task<Embed?> GetEmbedByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.Embeds.FirstOrDefaultAsync(e => e.Id == id);
    }

    /// <summary>
    ///     Updates the name and/or JSON of an existing embed template.
    /// </summary>
    /// <param name="id">The ID of the embed template to update.</param>
    /// <param name="embedName">The new name, or null to leave unchanged.</param>
    /// <param name="jsonCode">The new JSON representation, or null to leave unchanged.</param>
    /// <returns>A task that represents the asynchronous operation, containing the updated embed template if found.</returns>
    public async Task<Embed?> UpdateEmbedAsync(int id, string? embedName, string? jsonCode)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var embed = await db.Embeds.FirstOrDefaultAsync(e => e.Id == id);
        if (embed == null)
            return null;

        if (embedName != null)
            embed.EmbedName = embedName;
        if (jsonCode != null)
            embed.JsonCode = jsonCode;

        await db.UpdateAsync(embed);
        return embed;
    }

    /// <summary>
    ///     Deletes an embed template by its database ID.
    /// </summary>
    /// <param name="id">The ID of the embed template to delete.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating success.</returns>
    public async Task<bool> DeleteEmbedByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var embed = await db.Embeds.FirstOrDefaultAsync(e => e.Id == id);
        if (embed == null)
            return false;

        await db.DeleteAsync(embed);
        return true;
    }
}