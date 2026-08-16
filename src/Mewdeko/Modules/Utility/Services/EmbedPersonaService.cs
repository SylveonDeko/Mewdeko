using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Manages saved "send as" personas for the embed builder. A persona is a reusable display name and
///     avatar a message can be delivered under via webhook, either personal or shared with a guild.
/// </summary>
public class EmbedPersonaService(IDataConnectionFactory dbFactory) : INService
{
    /// <summary>
    ///     Retrieves every personal persona belonging to a user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The user's personal personas, oldest first.</returns>
    public async Task<List<EmbedWebhookPersona>> GetUserPersonasAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.EmbedWebhookPersonas
            .Where(persona => persona.UserId == userId && persona.GuildId == null)
            .OrderBy(persona => persona.Id)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves every persona shared with a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The guild's shared personas, oldest first.</returns>
    public async Task<List<EmbedWebhookPersona>> GetGuildPersonasAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.EmbedWebhookPersonas
            .Where(persona => persona.GuildId == guildId)
            .OrderBy(persona => persona.Id)
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves a single persona by its database ID.
    /// </summary>
    /// <param name="id">The persona's database ID.</param>
    /// <returns>The persona, or null when no persona has that ID.</returns>
    public async Task<EmbedWebhookPersona?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.EmbedWebhookPersonas.FirstOrDefaultAsync(persona => persona.Id == id);
    }

    /// <summary>
    ///     Whether a persona with the given name already exists in the same scope. Personal personas are
    ///     scoped to their owner, guild-shared ones to their guild.
    /// </summary>
    /// <param name="userId">The owner, used when checking a personal persona.</param>
    /// <param name="guildId">The guild, or null when checking a personal persona.</param>
    /// <param name="name">The name to check, compared case-insensitively.</param>
    /// <param name="excludeId">A persona to ignore, so renaming a persona does not collide with itself.</param>
    /// <returns>True when the name is already taken.</returns>
    public async Task<bool> NameExistsAsync(ulong userId, ulong? guildId, string name, int? excludeId = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var query = guildId.HasValue
            ? db.EmbedWebhookPersonas.Where(persona => persona.GuildId == guildId.Value)
            : db.EmbedWebhookPersonas.Where(persona => persona.UserId == userId && persona.GuildId == null);

        if (excludeId.HasValue)
            query = query.Where(persona => persona.Id != excludeId.Value);

        return await query.AnyAsync(persona => persona.Name.ToLower() == name.ToLower());
    }

    /// <summary>
    ///     Creates a persona.
    /// </summary>
    /// <param name="userId">The ID of the user creating it, and the owner of a personal persona.</param>
    /// <param name="guildId">The guild to share it with, or null for a personal persona.</param>
    /// <param name="name">The display name messages are sent under.</param>
    /// <param name="avatarUrl">An avatar URL, used as a per-message override.</param>
    /// <param name="avatarData">An uploaded avatar, baked onto the persona's per-channel webhooks.</param>
    /// <returns>The created persona.</returns>
    public async Task<EmbedWebhookPersona> CreateAsync(
        ulong userId, ulong? guildId, string name, string? avatarUrl, byte[]? avatarData)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var persona = new EmbedWebhookPersona
        {
            UserId = userId,
            GuildId = guildId,
            Name = name,
            AvatarUrl = avatarUrl,
            AvatarData = avatarData,
            AvatarVersion = 1,
            IsGuildShared = guildId.HasValue,
            DateAdded = DateTime.UtcNow
        };

        persona.Id = await db.InsertWithInt32IdentityAsync(persona);
        return persona;
    }

    /// <summary>
    ///     Updates a persona. Any change to the avatar bumps the version, which is how already-created
    ///     webhooks learn they need refreshing the next time the persona is used.
    /// </summary>
    /// <param name="id">The persona's database ID.</param>
    /// <param name="name">The new name, or null to leave unchanged.</param>
    /// <param name="avatarUrl">The new avatar URL, or null to leave unchanged.</param>
    /// <param name="avatarData">The new uploaded avatar, or null to leave unchanged.</param>
    /// <param name="clearAvatar">Whether to drop the existing avatar entirely, ignoring the two avatar fields.</param>
    /// <returns>The updated persona, or null when no persona has that ID.</returns>
    public async Task<EmbedWebhookPersona?> UpdateAsync(
        int id, string? name, string? avatarUrl, byte[]? avatarData, bool clearAvatar)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var persona = await db.EmbedWebhookPersonas.FirstOrDefaultAsync(entry => entry.Id == id);
        if (persona == null)
            return null;

        if (name != null)
            persona.Name = name;

        if (clearAvatar)
        {
            persona.AvatarUrl = null;
            persona.AvatarData = null;
            persona.AvatarVersion++;
        }
        else if (avatarData != null)
        {
            persona.AvatarData = avatarData;
            persona.AvatarUrl = null;
            persona.AvatarVersion++;
        }
        else if (avatarUrl != null)
        {
            persona.AvatarUrl = avatarUrl;
            persona.AvatarData = null;
            persona.AvatarVersion++;
        }

        await db.UpdateAsync(persona);
        return persona;
    }

    /// <summary>
    ///     Records the public URL an uploaded avatar was published under. Publishing happens after the
    ///     persona row exists, since the file name is keyed by the persona's ID.
    /// </summary>
    /// <param name="id">The persona's database ID.</param>
    /// <param name="avatarUrl">The published URL.</param>
    public async Task SetAvatarUrlAsync(int id, string avatarUrl)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.EmbedWebhookPersonas
            .Where(persona => persona.Id == id)
            .Set(persona => persona.AvatarUrl, avatarUrl)
            .UpdateAsync();
    }

    /// <summary>
    ///     Deletes a persona.
    /// </summary>
    /// <param name="id">The persona's database ID.</param>
    /// <returns>True when a persona was deleted.</returns>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.EmbedWebhookPersonas.Where(persona => persona.Id == id).DeleteAsync() > 0;
    }
}