using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Achievements.Common;

namespace Mewdeko.Modules.Achievements.Services;

public sealed partial class AchievementService
{
    /// <summary>
    ///     Most card designs a server keeps.
    /// </summary>
    public const int MaxCardDesigns = 20;

    /// <summary>
    ///     Longest card design name.
    /// </summary>
    public const int CardNameLength = 40;

    private readonly ConcurrentDictionary<ulong, IReadOnlyList<AchievementCardDesignInfo>> cardCache = new();

    /// <summary>
    ///     A server's saved card designs, oldest first.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The designs.</returns>
    public async Task<IReadOnlyList<AchievementCardDesignInfo>> GetCardDesignsAsync(ulong guildId)
    {
        if (cardCache.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var rows = await db.AchievementCardDesigns.Where(x => x.GuildId == guildId).OrderBy(x => x.Id).ToListAsync();
        var designs = rows.Select(Info).ToList();
        cardCache[guildId] = designs;
        return designs;
    }

    /// <summary>
    ///     The design an achievement is drawn with: its own pick, then its category's, then the server's default, then
    ///     the built in design. Picks naming a deleted design fall through.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="definition">The achievement.</param>
    /// <returns>The design.</returns>
    public async Task<AchievementCardTemplate> GetCardForAsync(ulong guildId, AchievementDefinition definition)
    {
        var settings = GetSettings(guildId);
        var designs = await GetCardDesignsAsync(guildId);
        var assignments = settings.CardAssignments;
        int?[] picks =
        [
            assignments.Achievements.TryGetValue(definition.Key, out var own) ? own : null,
            assignments.Categories.TryGetValue(definition.CategoryKey, out var category) ? category : null,
            settings.Row.DefaultCardId
        ];
        foreach (var pick in picks)
        {
            if (pick is { } id && designs.FirstOrDefault(d => d.Id == id) is { } design)
                return design.Template;
        }

        return AchievementCardTemplate.Default();
    }

    /// <summary>
    ///     Saves a new card design.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">Its name.</param>
    /// <param name="template">The design.</param>
    /// <returns>The design as saved.</returns>
    public async Task<AchievementResult<AchievementCardDesignInfo>> CreateCardDesignAsync(ulong guildId, string name,
        AchievementCardTemplate template)
    {
        name = name.Trim();
        if (name.Length is 0 or > CardNameLength)
            return AchievementResult<AchievementCardDesignInfo>.Fail(AchievementError.NameInvalid);
        if ((await GetCardDesignsAsync(guildId)).Count >= MaxCardDesigns)
            return AchievementResult<AchievementCardDesignInfo>.Fail(AchievementError.TooManyCards);

        var card = await OwnedCardAsync(guildId, template);
        var now = DateTime.UtcNow;
        var row = new AchievementCardDesign
        {
            GuildId = guildId,
            Name = name,
            Template = card.Serialize(),
            DateAdded = now,
            DateUpdated = now
        };
        await using var db = await dbFactory.CreateConnectionAsync();
        row.Id = await db.InsertWithInt32IdentityAsync(row);
        cardCache.TryRemove(guildId, out _);
        return AchievementResult<AchievementCardDesignInfo>.Ok(Info(row));
    }

    /// <summary>
    ///     Changes a saved card design.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <param name="name">Its new name, or null to keep it.</param>
    /// <param name="template">The new design, or null to keep it.</param>
    /// <returns>The design as saved.</returns>
    public async Task<AchievementResult<AchievementCardDesignInfo>> UpdateCardDesignAsync(ulong guildId, int id,
        string? name, AchievementCardTemplate? template)
    {
        name = name?.Trim();
        if (name is not null && name.Length is 0 or > CardNameLength)
            return AchievementResult<AchievementCardDesignInfo>.Fail(AchievementError.NameInvalid);

        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.AchievementCardDesigns.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Id == id);
        if (row is null)
            return AchievementResult<AchievementCardDesignInfo>.Fail(AchievementError.NotFound);

        if (name is not null)
            row.Name = name;
        if (template is not null)
            row.Template = (await OwnedCardAsync(guildId, template)).Serialize();
        row.DateUpdated = DateTime.UtcNow;
        await db.UpdateAsync(row);
        cardCache.TryRemove(guildId, out _);
        return AchievementResult<AchievementCardDesignInfo>.Ok(Info(row));
    }

    /// <summary>
    ///     Deletes a saved card design. Anything using it, including the server default, falls back.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <returns>True when it existed.</returns>
    public async Task<bool> DeleteCardDesignAsync(ulong guildId, int id)
    {
        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            if (await db.AchievementCardDesigns.Where(x => x.GuildId == guildId && x.Id == id).DeleteAsync() == 0)
                return false;
        }

        cardCache.TryRemove(guildId, out _);
        await UpdateSettingsAsync(guildId, row =>
        {
            if (row.DefaultCardId == id)
                row.DefaultCardId = null;
            var assignments = AchievementCardAssignments.Parse(row.CardAssignments);
            foreach (var key in assignments.Categories.Where(p => p.Value == id).Select(p => p.Key).ToList())
                assignments.Categories.Remove(key);
            foreach (var key in assignments.Achievements.Where(p => p.Value == id).Select(p => p.Key).ToList())
                assignments.Achievements.Remove(key);
            row.CardAssignments = assignments.Serialize();
        });
        return true;
    }

    /// <summary>
    ///     Sets the design every achievement uses unless its category or itself picks another.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID, or null for the built in design.</param>
    /// <returns>The error, if any.</returns>
    public async Task<AchievementError> SetDefaultCardAsync(ulong guildId, int? id)
    {
        if (id is { } designId && (await GetCardDesignsAsync(guildId)).All(d => d.Id != designId))
            return AchievementError.NotFound;
        await UpdateSettingsAsync(guildId, row => row.DefaultCardId = id);
        return AchievementError.None;
    }

    /// <summary>
    ///     Sets the design a category or achievement uses instead of the server's default.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="category">True for a category key, false for an achievement key.</param>
    /// <param name="key">The category or achievement key.</param>
    /// <param name="id">The design ID, or null to follow the default again.</param>
    /// <returns>The error, if any.</returns>
    public async Task<AchievementError> AssignCardAsync(ulong guildId, bool category, string key, int? id)
    {
        var catalog = await GetCatalogAsync(guildId);
        var known = category ? catalog.Categories.Any(c => c.Key == key) : catalog.ByKey.ContainsKey(key);
        if (!known)
            return category ? AchievementError.CategoryInvalid : AchievementError.NotFound;
        if (id is { } designId && (await GetCardDesignsAsync(guildId)).All(d => d.Id != designId))
            return AchievementError.NotFound;

        await UpdateSettingsAsync(guildId, row =>
        {
            var assignments = AchievementCardAssignments.Parse(row.CardAssignments);
            var map = category ? assignments.Categories : assignments.Achievements;
            if (id is { } value)
                map[key] = value;
            else
                map.Remove(key);
            row.CardAssignments = assignments.Serialize();
        });
        return AchievementError.None;
    }

    /// <summary>
    ///     Brings a card design within limits and drops links to uploads the server doesn't own, without saving it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="template">The design.</param>
    /// <returns>The corrected design.</returns>
    public async Task<AchievementCardTemplate> OwnedCardAsync(ulong guildId, AchievementCardTemplate template)
    {
        var card = AchievementCardRules.Sanitize(template);
        var linked = AchievementCardRules.Uploads(card).Distinct().ToList();
        if (linked.Count == 0)
            return card;

        await using var db = await dbFactory.CreateConnectionAsync();
        var owned = (await db.AchievementIconUploads
            .Where(x => x.GuildId == guildId && linked.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync()).ToHashSet();

        bool Missing(string link)
        {
            return AchievementIcons.UploadId(AchievementIcons.Parse(link)) is { } id && !owned.Contains(id);
        }

        if (Missing(card.Background.Url))
            card.Background.Url = "";
        foreach (var element in card.Elements.Where(e => Missing(e.Url)))
            element.Url = "";
        return card;
    }

    /// <summary>
    ///     Removes a deleted upload from every design that links to it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="uploadId">The deleted upload.</param>
    public async Task DropCardUploadAsync(ulong guildId, int uploadId)
    {
        foreach (var design in await GetCardDesignsAsync(guildId))
        {
            if (AchievementCardRules.Uploads(design.Template).Contains(uploadId))
                await UpdateCardDesignAsync(guildId, design.Id, null, design.Template.Clone());
        }
    }

    private static AchievementCardDesignInfo Info(AchievementCardDesign row)
    {
        return new AchievementCardDesignInfo(row.Id, row.Name, AchievementCardTemplate.Parse(row.Template),
            row.DateUpdated);
    }
}
