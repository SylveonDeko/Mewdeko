using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Common.Cards;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Achievements.Services;
using Mewdeko.Modules.CardDesigns.Common;
using Mewdeko.Modules.Xp.Events;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Mewdeko.Modules.CardDesigns.Services;

/// <summary>
///     Why a design change was refused.
/// </summary>
public enum CardDesignError
{
    /// <summary>
    ///     Nothing went wrong.
    /// </summary>
    None,

    /// <summary>
    ///     The design doesn't exist in the guild.
    /// </summary>
    NotFound,

    /// <summary>
    ///     Another design in the guild has that name.
    /// </summary>
    NameTaken
}

/// <summary>
///     The result of equipping a design.
/// </summary>
public enum EquipResult
{
    /// <summary>
    ///     The design is equipped.
    /// </summary>
    Equipped,

    /// <summary>
    ///     The design doesn't exist in the guild.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The member neither owns it nor can use it for free.
    /// </summary>
    NotOwned
}

/// <summary>
///     A design with the figures the dashboard lists next to it.
/// </summary>
/// <param name="Design">The design.</param>
/// <param name="OwnedCount">How many members own it.</param>
/// <param name="ShopItemId">The shop item that sells it, if any.</param>
/// <param name="ShopItemName">The name of that item.</param>
public sealed record CardDesignInfo(CardDesign Design, int OwnedCount, int? ShopItemId, string? ShopItemName);

/// <summary>
///     Stores community card designs with their owners and equips, and picks the design a card is drawn with.
/// </summary>
public sealed partial class CardDesignService : INService
{
    private const string EventModule = "CardDesigns";

    private readonly ConcurrentDictionary<ulong, IReadOnlyList<CardDesign>> cache = new();
    private readonly ConcurrentDictionary<ulong, IReadOnlyList<CardDesign>> memberCache = new();
    private readonly ConcurrentDictionary<ulong, CardDesignSetting> settingsCache = new();
    private readonly IDataConnectionFactory dbFactory;
    private readonly DiscordShardedClient client;
    private readonly ILogger<CardDesignService> logger;
    private readonly IServiceProvider services;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CardDesignService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The event handler, for level ups, achievement unlocks and role changes.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="services">The service provider, for services resolved when needed.</param>
    /// <param name="logger">The logger.</param>
    public CardDesignService(IDataConnectionFactory dbFactory, EventHandler eventHandler, DiscordShardedClient client,
        IServiceProvider services, ILogger<CardDesignService> logger)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.services = services;
        this.logger = logger;

        eventHandler.Subscribe("XpLevelChanged", EventModule, OnLevelChangedAsync);
        eventHandler.Subscribe("AchievementsUnlocked", EventModule, OnAchievementsUnlockedAsync);
        eventHandler.Subscribe("GuildMemberUpdated", EventModule, OnMemberUpdatedAsync);
    }

    #region Designs

    /// <summary>
    ///     Gets a guild's designs, oldest first.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The designs.</returns>
    public async Task<IReadOnlyList<CardDesign>> GetDesignsAsync(ulong guildId)
    {
        if (cache.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var designs = await db.CardDesigns.Where(x => x.GuildId == guildId && x.OwnerUserId == null)
            .OrderBy(x => x.Id)
            .ToListAsync();
        cache[guildId] = designs;
        return designs;
    }

    /// <summary>
    ///     Gets one design.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <returns>The design, or null.</returns>
    public async Task<CardDesign?> GetDesignAsync(ulong guildId, int id)
    {
        return (await GetDesignsAsync(guildId)).FirstOrDefault(d => d.Id == id);
    }

    /// <summary>
    ///     Gets a guild's designs with owner counts and the shop item selling each.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The designs with their figures.</returns>
    public async Task<IReadOnlyList<CardDesignInfo>> GetInfosAsync(ulong guildId)
    {
        var designs = await GetDesignsAsync(guildId);
        await using var db = await dbFactory.CreateConnectionAsync();

        var counts = (await db.CardDesignOwners.Where(x => x.GuildId == guildId)
                .GroupBy(x => x.DesignId)
                .Select(g => new
                {
                    DesignId = g.Key, Count = g.Count()
                })
                .ToListAsync())
            .ToDictionary(x => x.DesignId, x => x.Count);

        var items = (await db.ShopItems.Where(x => x.GuildId == guildId && x.DesignId != null)
                .Select(x => new
                {
                    x.Id, x.Name, x.DesignId
                })
                .ToListAsync())
            .GroupBy(x => x.DesignId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return designs.Select(d =>
        {
            items.TryGetValue(d.Id, out var item);
            return new CardDesignInfo(d, counts.GetValueOrDefault(d.Id), item?.Id, item?.Name);
        }).ToList();
    }

    /// <summary>
    ///     Creates a design and grants it to everyone who already meets its unlock condition.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="draft">The design to store. Its guild and ID are set here.</param>
    /// <returns>The stored design, or why it was refused.</returns>
    public async Task<(CardDesign? Design, CardDesignError Error)> CreateAsync(ulong guildId, CardDesign draft)
    {
        draft.GuildId = guildId;
        draft.Name = draft.Name.Trim();
        draft.IsDefault = false;
        draft.DateAdded = draft.DateUpdated = DateTime.UtcNow;
        CardDesignRules.ClearUnusedUnlock(draft);

        if (CardDesignRules.FindByName(await GetDesignsAsync(guildId), draft.Name) is not null)
            return (null, CardDesignError.NameTaken);

        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            try
            {
                draft.Id = await db.InsertWithInt32IdentityAsync(draft);
            }
            catch (Exception ex) when (IsDuplicate(ex))
            {
                return (null, CardDesignError.NameTaken);
            }
        }

        cache.TryRemove(guildId, out _);
        await BackfillAsync(draft);
        return (draft, CardDesignError.None);
    }

    /// <summary>
    ///     Changes a design and grants it to everyone who now meets its unlock condition.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <param name="mutate">An action applying the changes.</param>
    /// <returns>The stored design, or why it was refused.</returns>
    public async Task<(CardDesign? Design, CardDesignError Error)> UpdateAsync(ulong guildId, int id,
        Action<CardDesign> mutate)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var design = await db.CardDesigns.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Id == id);
        if (design is null)
            return (null, CardDesignError.NotFound);

        var before = design.Data;
        mutate(design);
        design.Name = design.Name.Trim();
        design.DateUpdated = DateTime.UtcNow;
        CardDesignRules.ClearUnusedUnlock(design);

        var others = (await GetDesignsAsync(guildId)).Where(d => d.Id != id);
        if (CardDesignRules.FindByName(others, design.Name) is not null)
            return (null, CardDesignError.NameTaken);

        try
        {
            await db.UpdateAsync(design);
        }
        catch (Exception ex) when (IsDuplicate(ex))
        {
            return (null, CardDesignError.NameTaken);
        }

        cache.TryRemove(guildId, out _);
        await BackfillAsync(design);
        if (before != design.Data)
            await ReleaseUnusedUploadsAsync(guildId, UploadsOf(before, (CardDesignTarget)design.Target), id);
        return (design, CardDesignError.None);
    }

    /// <summary>
    ///     Deletes a design. Ownership and equips go with it and shop items selling it lose their link.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <returns>True when it existed.</returns>
    public async Task<bool> DeleteAsync(ulong guildId, int id)
    {
        var design = await GetDesignAsync(guildId, id);
        if (design is null)
            return false;

        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            await db.ShopItems.Where(x => x.GuildId == guildId && x.DesignId == id)
                .Set(x => x.Enabled, false)
                .UpdateAsync();

            if (await db.CardDesigns.Where(x => x.GuildId == guildId && x.Id == id).DeleteAsync() == 0)
                return false;
        }

        cache.TryRemove(guildId, out _);
        await ReleaseUnusedUploadsAsync(guildId, UploadsOf(design.Data, (CardDesignTarget)design.Target), id);
        return true;
    }

    /// <summary>
    ///     Makes a design the community default for its target, or clears the default.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <param name="isDefault">Whether it should be the default.</param>
    /// <returns>The error, if any.</returns>
    public async Task<CardDesignError> SetDefaultAsync(ulong guildId, int id, bool isDefault)
    {
        var design = await GetDesignAsync(guildId, id);
        if (design is null)
            return CardDesignError.NotFound;

        await using var db = await dbFactory.CreateConnectionAsync();
        await using var tx = await db.BeginTransactionAsync();
        await db.CardDesigns.Where(x => x.GuildId == guildId && x.Target == design.Target && x.IsDefault)
            .Set(x => x.IsDefault, false)
            .UpdateAsync();
        if (isDefault)
            await db.CardDesigns.Where(x => x.GuildId == guildId && x.Id == id).Set(x => x.IsDefault, true).UpdateAsync();
        await tx.CommitAsync();

        cache.TryRemove(guildId, out _);
        return CardDesignError.None;
    }

    /// <summary>
    ///     Gets the community default design for a target.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">The target.</param>
    /// <returns>The design, or null.</returns>
    public async Task<CardDesign?> GetDefaultDesignAsync(ulong guildId, CardDesignTarget target)
    {
        return (await GetDesignsAsync(guildId)).FirstOrDefault(d => d.IsDefault && d.Target == (short)target);
    }

    #endregion

    #region Ownership

    /// <summary>
    ///     Gives a member a design they don't have yet.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="designId">The design ID.</param>
    /// <param name="source">How they got it.</param>
    /// <returns>True when it was newly granted.</returns>
    public async Task<bool> GrantAsync(ulong guildId, ulong userId, int designId, CardDesignSource source)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        if (await db.CardDesignOwners.AnyAsync(x => x.GuildId == guildId && x.UserId == userId && x.DesignId == designId))
            return false;

        try
        {
            await db.InsertAsync(new CardDesignOwner
            {
                GuildId = guildId,
                UserId = userId,
                DesignId = designId,
                Source = (short)source,
                DateAdded = DateTime.UtcNow
            });
            return true;
        }
        catch (Exception ex) when (IsDuplicate(ex))
        {
            return false;
        }
    }

    /// <summary>
    ///     Takes a design away from a member, unequipping it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="designId">The design ID.</param>
    public async Task RevokeAsync(ulong guildId, ulong userId, int designId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.CardDesignOwners.Where(x => x.GuildId == guildId && x.UserId == userId && x.DesignId == designId)
            .DeleteAsync();
        await db.EquippedCardDesigns
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.DesignId == designId)
            .DeleteAsync();
    }

    /// <summary>
    ///     Whether a member owns a design.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="designId">The design ID.</param>
    /// <returns>True when they own it.</returns>
    public async Task<bool> OwnsAsync(ulong guildId, ulong userId, int designId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.CardDesignOwners.AnyAsync(x =>
            x.GuildId == guildId && x.UserId == userId && x.DesignId == designId);
    }

    /// <summary>
    ///     Gets how a member came to own each of their designs.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>The source of each owned design, by design ID.</returns>
    public async Task<Dictionary<int, CardDesignSource>> GetOwnedAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return (await db.CardDesignOwners.Where(x => x.GuildId == guildId && x.UserId == userId)
                .Select(x => new
                {
                    x.DesignId, x.Source
                })
                .ToListAsync())
            .ToDictionary(x => x.DesignId, x => (CardDesignSource)x.Source);
    }

    /// <summary>
    ///     Grants every design a member qualifies for at their current standing.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="level">Their level, when known.</param>
    /// <param name="achievements">Their achievement keys, when known.</param>
    /// <param name="roles">Their role IDs, when known.</param>
    /// <param name="source">How they qualify.</param>
    /// <returns>How many designs were newly granted.</returns>
    public async Task<int> GrantQualifyingAsync(ulong guildId, ulong userId, int? level,
        IReadOnlyCollection<string>? achievements, IReadOnlyCollection<ulong>? roles, CardDesignSource source)
    {
        var matching = (await GetDesignsAsync(guildId))
            .Where(d => CardDesignRules.Unlocks(d, level, achievements, roles))
            .ToList();
        if (matching.Count == 0)
            return 0;

        var granted = 0;
        foreach (var design in matching)
        {
            if (await GrantAsync(guildId, userId, design.Id, source))
                granted++;
        }

        return granted;
    }

    /// <summary>
    ///     Grants an unlock design to every member who already meets its condition.
    /// </summary>
    /// <param name="design">The design.</param>
    /// <returns>How many members were newly granted it.</returns>
    public async Task<int> BackfillAsync(CardDesign design)
    {
        if (design.Availability != (short)CardDesignAvailability.Unlock)
            return 0;

        try
        {
            List<ulong> qualifying;
            CardDesignSource source;
            switch ((CardDesignUnlockType?)design.UnlockType)
            {
                case CardDesignUnlockType.Level when design.UnlockLevel is { } level:
                {
                    var curve = (XpCurveType)(await services.GetRequiredService<XpService>()
                        .GetGuildXpSettingsAsync(design.GuildId)).XpCurveType;
                    var needed = XpCalculator.CalculateXpForLevel(level, curve);
                    await using var db = await dbFactory.CreateConnectionAsync();
                    qualifying = await db.GuildUserXps.Where(x => x.GuildId == design.GuildId && x.TotalXp >= needed)
                        .Select(x => x.UserId)
                        .ToListAsync();
                    source = CardDesignSource.Level;
                    break;
                }
                case CardDesignUnlockType.Achievement when design.UnlockAchievementKey is { } key:
                {
                    await using var db = await dbFactory.CreateConnectionAsync();
                    qualifying = await db.UserAchievements
                        .Where(x => x.GuildId == design.GuildId && x.AchievementKey == key)
                        .Select(x => x.UserId)
                        .ToListAsync();
                    source = CardDesignSource.Achievement;
                    break;
                }
                case CardDesignUnlockType.Role when design.UnlockRoleId is { } roleId:
                    qualifying = client.GetGuild(design.GuildId)?.GetRole(roleId)?.Members.Select(m => m.Id).ToList() ??
                                 [];
                    source = CardDesignSource.Role;
                    break;
                default:
                    return 0;
            }

            return await InsertOwnersAsync(design, qualifying.Distinct().ToList(), source);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not backfill card design {DesignId} in {GuildId}", design.Id, design.GuildId);
            return 0;
        }
    }

    private async Task<int> InsertOwnersAsync(CardDesign design, List<ulong> userIds, CardDesignSource source)
    {
        if (userIds.Count == 0)
            return 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = (await db.CardDesignOwners.Where(x => x.GuildId == design.GuildId && x.DesignId == design.Id)
            .Select(x => x.UserId)
            .ToListAsync()).ToHashSet();

        var now = DateTime.UtcNow;
        var rows = userIds.Where(id => !existing.Contains(id))
            .Select(id => new CardDesignOwner
            {
                GuildId = design.GuildId,
                UserId = id,
                DesignId = design.Id,
                Source = (short)source,
                DateAdded = now
            })
            .ToList();
        if (rows.Count == 0)
            return 0;

        await db.BulkCopyAsync(new BulkCopyOptions
        {
            BulkCopyType = BulkCopyType.MultipleRows
        }, rows);
        return rows.Count;
    }

    #endregion

    #region Equipping

    /// <summary>
    ///     Equips a design for its target.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="designId">The design ID.</param>
    /// <returns>The outcome.</returns>
    public async Task<EquipResult> EquipAsync(ulong guildId, ulong userId, int designId)
    {
        var design = await GetDesignAsync(guildId, designId) ?? await GetMemberDesignByIdAsync(guildId, designId);
        if (design is null)
            return EquipResult.NotFound;

        if (design.OwnerUserId is not null)
        {
            if (design.OwnerUserId != userId || !await IsMemberUsableAsync(design))
                return EquipResult.NotOwned;
        }
        else
        {
            var owns = design.Availability == (short)CardDesignAvailability.Free ||
                       await OwnsAsync(guildId, userId, designId);
            if (!CardDesignRules.CanEquip(design, owns))
                return EquipResult.NotOwned;
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var updated = await db.EquippedCardDesigns
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.Target == design.Target)
            .Set(x => x.DesignId, designId)
            .UpdateAsync();
        if (updated > 0)
            return EquipResult.Equipped;

        try
        {
            await db.InsertAsync(new EquippedCardDesign
            {
                GuildId = guildId, UserId = userId, Target = design.Target, DesignId = designId
            });
        }
        catch (Exception ex) when (IsDuplicate(ex))
        {
            await db.EquippedCardDesigns
                .Where(x => x.GuildId == guildId && x.UserId == userId && x.Target == design.Target)
                .Set(x => x.DesignId, designId)
                .UpdateAsync();
        }

        return EquipResult.Equipped;
    }

    /// <summary>
    ///     Unequips a member's design for a target.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="target">The target.</param>
    /// <returns>True when they had one equipped.</returns>
    public async Task<bool> UnequipAsync(ulong guildId, ulong userId, CardDesignTarget target)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.EquippedCardDesigns
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.Target == (short)target)
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Gets the design IDs a member has equipped.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>The equipped design ID for each target.</returns>
    public async Task<Dictionary<CardDesignTarget, int>> GetEquippedIdsAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return (await db.EquippedCardDesigns.Where(x => x.GuildId == guildId && x.UserId == userId)
                .Select(x => new
                {
                    x.Target, x.DesignId
                })
                .ToListAsync())
            .ToDictionary(x => (CardDesignTarget)x.Target, x => x.DesignId);
    }

    /// <summary>
    ///     Gets the design a member has equipped for a target, if they can still use it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="target">The target.</param>
    /// <returns>The design, or null.</returns>
    public async Task<CardDesign?> GetEquippedDesignAsync(ulong guildId, ulong userId, CardDesignTarget target)
    {
        var equipped = await GetEquippedIdsAsync(guildId, userId);
        if (!equipped.TryGetValue(target, out var id))
            return null;

        if (await GetMemberDesignByIdAsync(guildId, id) is { } own)
            return own.OwnerUserId == userId && await IsMemberUsableAsync(own) ? own : null;

        if (await GetDesignAsync(guildId, id) is not { } design)
            return null;

        return CardDesignRules.CanEquip(design, await OwnsAsync(guildId, userId, id)) ? design : null;
    }

    /// <summary>
    ///     Picks the design a member's card is drawn with: their equipped design, else the community default.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="target">The target.</param>
    /// <returns>The design, or null for the built in one.</returns>
    public async Task<CardDesign?> ResolveAsync(ulong guildId, ulong userId, CardDesignTarget target)
    {
        return await GetEquippedDesignAsync(guildId, userId, target) ?? await GetDefaultDesignAsync(guildId, target);
    }

    /// <summary>
    ///     Gets the designs a member can equip: free ones plus the ones they own.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>The designs, with how each was obtained (null for free ones).</returns>
    public async Task<IReadOnlyList<(CardDesign Design, CardDesignSource? Source)>> GetAvailableAsync(ulong guildId,
        ulong userId)
    {
        var owned = await GetOwnedAsync(guildId, userId);
        return (await GetDesignsAsync(guildId))
            .Where(d => CardDesignRules.CanEquip(d, owned.ContainsKey(d.Id)))
            .Select(d => (d, owned.TryGetValue(d.Id, out var source) ? (CardDesignSource?)source : null))
            .ToList();
    }

    #endregion

    #region Rendering

    /// <summary>
    ///     Loads a rank design's background, fitted to the rank card.
    /// </summary>
    /// <param name="design">The design.</param>
    /// <returns>The image, or null when it has none or it can't be loaded.</returns>
    public async Task<byte[]?> LoadRankBackgroundAsync(CardDesign design)
    {
        var reference = CardDesignRules.BackgroundRef(CardDesignTarget.Rank, CardDesignRules.EffectiveData(design));
        if (reference is null)
            return null;

        var backgrounds = services.GetRequiredService<XpBackgroundService>();
        if (reference.StartsWith(AchievementIcons.UploadPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AchievementIcons.UploadId(AchievementIcons.Parse(reference)) is { } id &&
                   await services.GetRequiredService<AchievementIconService>().GetBytesAsync(design.GuildId, id) is
                       { } raw
                ? backgrounds.Fit(raw)
                : null;
        }

        var loaded = await backgrounds.LoadAsync(reference);
        return loaded is null || CardPresets.TryParse(reference, out _) ? loaded : backgrounds.Fit(loaded);
    }

    /// <summary>
    ///     Loads the background image of an achievement design, for the profile card.
    /// </summary>
    /// <param name="design">The design.</param>
    /// <returns>The image, or null when it has none or it can't be loaded.</returns>
    public async Task<SKBitmap?> LoadAchievementBackdropAsync(CardDesign design)
    {
        var reference = CardDesignRules.BackgroundRef(CardDesignTarget.Achievement,
            CardDesignRules.EffectiveData(design));
        if (reference is null)
            return null;

        if (CardPresets.TryParse(reference, out _))
            return await services.GetRequiredService<CardPresetService>().LoadBitmapAsync(reference);

        return await services.GetRequiredService<AchievementIconService>()
            .LoadImageAsync(design.GuildId, AchievementIcons.Parse(reference));
    }

    /// <summary>
    ///     Reads an achievement design as a card template.
    /// </summary>
    /// <param name="design">The design.</param>
    /// <returns>The template.</returns>
    public static CardTemplate AchievementTemplate(CardDesign design)
    {
        return AchievementCards.Parse(CardDesignRules.EffectiveData(design));
    }

    /// <summary>
    ///     Works out an address a browser can load for each design's background.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="designs">The designs.</param>
    /// <returns>The address by design ID, null where a design has no image background.</returns>
    public async Task<Dictionary<int, string?>> ResolveBackgroundUrlsAsync(ulong guildId,
        IEnumerable<CardDesign> designs)
    {
        var list = designs.ToList();
        var references = list.ToDictionary(d => d.Id,
            d => CardDesignRules.BackgroundRef((CardDesignTarget)d.Target, d.Data));
        var result = new Dictionary<int, string?>();
        if (references.Values.All(r => r is null))
        {
            foreach (var design in list)
                result[design.Id] = null;
            return result;
        }

        var presets = services.GetRequiredService<CardPresetService>();
        var uploadIds = references.Values.Where(r => r is not null)
            .Select(r => AchievementIcons.UploadId(AchievementIcons.Parse(r)))
            .OfType<int>()
            .Distinct()
            .ToList();
        var uploadUrls = new Dictionary<int, string?>();
        if (uploadIds.Count > 0)
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            uploadUrls = (await db.AchievementIconUploads
                    .Where(x => x.GuildId == guildId && uploadIds.Contains(x.Id))
                    .Select(x => new
                    {
                        x.Id, x.PublicUrl
                    })
                    .ToListAsync())
                .ToDictionary(x => x.Id, x => x.PublicUrl);
        }

        foreach (var design in list)
        {
            var reference = references[design.Id];
            if (reference is null)
            {
                result[design.Id] = null;
            }
            else if (CardPresets.TryParse(reference, out var presetId))
            {
                var preset = await presets.FindAsync(presetId);
                result[design.Id] = preset is null ? null : CardPresets.Url(presets.BaseUrl, preset.Image);
            }
            else if (AchievementIcons.UploadId(AchievementIcons.Parse(reference)) is { } uploadId)
            {
                result[design.Id] = uploadUrls.GetValueOrDefault(uploadId) ??
                                    AchievementService.IconUploadPath(guildId, uploadId);
            }
            else
            {
                result[design.Id] = reference;
            }
        }

        return result;
    }

    #endregion

    #region Data

    /// <summary>
    ///     Checks and cleans the data an admin sent for a design.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">The design's target.</param>
    /// <param name="raw">The data as sent, or null for an empty design.</param>
    /// <returns>The data to store, or what is wrong with it.</returns>
    public async Task<(string? Data, string? Error)> NormalizeDataAsync(ulong guildId, CardDesignTarget target,
        JsonElement? raw)
    {
        if (target == CardDesignTarget.Rank)
        {
            string? reference = null;
            if (raw is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("background", out var property))
            {
                if (property.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                    return (null, "Background must be text.");
                reference = property.GetString()?.Trim();
            }

            return await CheckReferenceAsync(guildId, reference) is { } error
                ? (null, error)
                : (CardDesignRules.RankData(reference), null);
        }

        CardTemplate template;
        try
        {
            template = raw is { ValueKind: JsonValueKind.Object } body
                ? JsonSerializer.Deserialize<CardTemplate>(body.GetRawText(), CardTemplate.Json) ??
                  AchievementCards.Default()
                : AchievementCards.Default();
        }
        catch (JsonException)
        {
            return (null, "That isn't a card design.");
        }

        var owned = await services.GetRequiredService<AchievementService>().OwnedCardAsync(guildId, template);
        return await CheckReferenceAsync(guildId, owned.Background.Url) is { } problem
            ? (null, problem)
            : (owned.Serialize(), null);
    }

    /// <summary>
    ///     Checks that a background reference is allowed: a preset in the catalog, a card image the guild uploaded, or a
    ///     web address.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="reference">The reference.</param>
    /// <returns>What is wrong with it, or null.</returns>
    public async Task<string?> CheckReferenceAsync(ulong guildId, string? reference)
    {
        var value = reference?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;
        if (!CardDesignRules.IsValidBackgroundRef(value))
            return "Background must be a preset, an uploaded image or a web address.";
        if (CardPresets.TryParse(value, out _))
            return await services.GetRequiredService<CardPresetService>().ExistsAsync(value)
                ? null
                : "That preset doesn't exist.";
        if (AchievementIcons.UploadId(AchievementIcons.Parse(value)) is not { } id)
            return null;

        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.AchievementIconUploads.AnyAsync(x =>
            x.GuildId == guildId && x.Id == id && x.Kind == AchievementIconService.CardKind)
            ? null
            : "That image isn't one of this community's uploads.";
    }

    #endregion

    #region Uploads

    /// <summary>
    ///     Removes a deleted upload from every design that links to it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="uploadId">The deleted upload.</param>
    public async Task DropUploadAsync(ulong guildId, int uploadId)
    {
        foreach (var design in await GetDesignsAsync(guildId))
        {
            if (!UploadsOf(design.Data, (CardDesignTarget)design.Target).Contains(uploadId))
                continue;

            var target = (CardDesignTarget)design.Target;
            string cleaned;
            if (target == CardDesignTarget.Rank)
            {
                cleaned = CardDesignRules.RankData(null);
            }
            else
            {
                var template = AchievementCards.Parse(design.Data);
                var link = AchievementIcons.UploadPrefix + uploadId;
                if (template.Background.Url == link)
                    template.Background.Url = "";
                foreach (var element in template.Elements.Where(e => e.Url == link))
                    element.Url = "";
                cleaned = template.Serialize();
            }

            await using var db = await dbFactory.CreateConnectionAsync();
            await db.CardDesigns.Where(x => x.Id == design.Id).Set(x => x.Data, cleaned).UpdateAsync();
        }

        cache.TryRemove(guildId, out _);
        await DropMemberUploadAsync(guildId, uploadId);
    }

    private static HashSet<int> UploadsOf(string? data, CardDesignTarget target)
    {
        if (target == CardDesignTarget.Rank)
        {
            return CardDesignRules.BackgroundRef(target, data) is { } reference &&
                   AchievementIcons.UploadId(AchievementIcons.Parse(reference)) is { } id
                ? [id]
                : [];
        }

        return string.IsNullOrWhiteSpace(data) ? [] : CardRules.Uploads(AchievementCards.Parse(data)).ToHashSet();
    }

    private async Task ReleaseUnusedUploadsAsync(ulong guildId, HashSet<int> candidates, int exceptDesignId)
    {
        if (candidates.Count == 0)
            return;

        try
        {
            var designs = (await GetDesignsAsync(guildId)).Where(d => d.Id != exceptDesignId).ToList();
            var current = (await GetDesignAsync(guildId, exceptDesignId)) is { } self
                ? UploadsOf(self.Data, (CardDesignTarget)self.Target)
                : [];
            var used = designs.SelectMany(d => UploadsOf(d.Data, (CardDesignTarget)d.Target))
                .Concat(current)
                .ToHashSet();
            var achievements = services.GetRequiredService<AchievementService>();
            foreach (var design in await achievements.GetCardDesignsAsync(guildId))
                used.UnionWith(CardRules.Uploads(design.Template));

            var icons = services.GetRequiredService<AchievementIconService>();
            foreach (var id in candidates.Where(id => !used.Contains(id)))
                await icons.DeleteAsync(guildId, id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not release card design uploads in {GuildId}", guildId);
        }
    }

    #endregion

    #region Events

    private async Task OnLevelChangedAsync(XpLevelChangedEventArgs args)
    {
        if (!args.IsLevelUp)
            return;

        try
        {
            await GrantQualifyingAsync(args.GuildId, args.UserId, args.NewLevel, null, null, CardDesignSource.Level);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not grant level designs to {UserId} in {GuildId}", args.UserId, args.GuildId);
        }
    }

    private async Task OnAchievementsUnlockedAsync(AchievementsUnlockedEventArgs args)
    {
        try
        {
            await GrantQualifyingAsync(args.GuildId, args.UserId, null, args.Keys, null,
                CardDesignSource.Achievement);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not grant achievement designs to {UserId} in {GuildId}", args.UserId,
                args.GuildId);
        }
    }

    private async Task OnMemberUpdatedAsync(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
    {
        if (after.IsBot)
            return;

        try
        {
            var afterRoles = after.Roles.Select(r => r.Id).ToHashSet();
            var added = before.HasValue
                ? afterRoles.Except(before.Value.Roles.Select(r => r.Id)).ToList()
                : afterRoles.ToList();
            if (added.Count == 0)
                return;

            if (!(await GetDesignsAsync(after.Guild.Id)).Any(d =>
                    d.UnlockType == (short)CardDesignUnlockType.Role && d.UnlockRoleId is { } role &&
                    added.Contains(role)))
                return;

            await GrantQualifyingAsync(after.Guild.Id, after.Id, null, null, added, CardDesignSource.Role);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not grant role designs to {UserId} in {GuildId}", after.Id, after.Guild.Id);
        }
    }

    #endregion

    private static bool IsDuplicate(Exception ex)
    {
        return ex.GetType().Name == "PostgresException" && ex.Message.Contains("23505");
    }
}
