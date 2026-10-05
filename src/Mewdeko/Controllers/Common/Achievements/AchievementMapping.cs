using DataModel;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Achievements.Services;

namespace Mewdeko.Controllers.Common.Achievements;

/// <summary>
///     Turns achievement models into API responses.
/// </summary>
public static class AchievementMapping
{
    /// <summary>
    ///     Maps an achievement.
    /// </summary>
    /// <param name="def">The resolved achievement.</param>
    /// <param name="catalog">The server's catalog, for icons.</param>
    /// <param name="guild">The guild, for role names.</param>
    /// <param name="counts">Unlock counts by key.</param>
    /// <param name="overrides">Stored overrides by key.</param>
    /// <param name="customs">Stored server made achievements by ID.</param>
    /// <returns>The response.</returns>
    public static AchievementResponse Map(AchievementDefinition def, AchievementGuildCatalog catalog, SocketGuild? guild,
        IReadOnlyDictionary<string, int>? counts, IReadOnlyDictionary<string, AchievementOverride>? overrides,
        IReadOnlyDictionary<int, CustomAchievement>? customs)
    {
        var icon = catalog.IconFor(def);
        var builtIn = def.IsCustom ? null : AchievementCatalog.GetBuiltIn(def.Key);
        AchievementOverride? o = null;
        CustomAchievement? custom = null;
        overrides?.TryGetValue(def.Key, out o);
        if (def.IsCustom)
            customs?.TryGetValue(def.CustomId, out custom);

        return new AchievementResponse
        {
            Key = def.Key,
            CategoryKey = def.CategoryKey,
            Name = def.Name,
            Description = def.Description,
            Icon = icon.Stored,
            IconUrl = catalog.ImageUrl(icon),
            Grade = (int)def.Grade,
            Points = def.Points,
            Hidden = def.Hidden,
            Enabled = def.Enabled,
            SelfEnabled = def.SelfEnabled,
            Trigger = (int)def.Trigger,
            Metric = (int)def.Metric,
            Threshold = def.Threshold,
            Keyword = def.Trigger is AchievementTrigger.Keyword or AchievementTrigger.Reaction ? def.Keyword : null,
            ChannelId = def.ChannelId,
            RoleRewardId = def.RoleRewardId,
            RoleRewardName = def.RoleRewardId is { } roleId ? guild?.GetRole(roleId)?.Name : null,
            CurrencyReward = def.CurrencyReward,
            XpReward = def.XpReward,
            IsCustom = def.IsCustom,
            CustomId = def.IsCustom ? def.CustomId : null,
            IsGlobal = def.IsGlobal,
            IsOverridden = def.IsOverridden,
            UnlockCount = counts?.GetValueOrDefault(def.Key) ?? 0,
            DefaultName = builtIn?.Name,
            DefaultDescription = builtIn?.Description,
            DefaultPoints = builtIn?.Points,
            DefaultHidden = builtIn?.Hidden,
            RawName = o?.Name,
            RawDescription = def.IsCustom ? custom?.Description : o?.Description,
            RawIcon = def.IsCustom ? custom?.Icon : o?.Icon,
            RawPoints = def.IsCustom ? custom?.Points : o?.Points
        };
    }

    /// <summary>
    ///     Maps categories with counts.
    /// </summary>
    /// <param name="catalog">The server's catalog.</param>
    /// <param name="settings">The server's settings.</param>
    /// <returns>The responses.</returns>
    public static List<AchievementCategoryResponse> MapCategories(AchievementGuildCatalog catalog,
        AchievementGuildSettings settings)
    {
        return catalog.Categories.Select(c =>
        {
            var items = catalog.All.Where(d => d.CategoryKey == c.Key).ToList();
            return new AchievementCategoryResponse
            {
                Key = c.Key,
                Name = c.Name,
                Icon = c.Icon,
                IconUrl = catalog.ImageUrl(AchievementIcons.Parse(c.Icon)),
                Description = c.Description,
                IsBuiltIn = c.IsBuiltIn,
                HasBadges = c.HasBadges,
                Enabled = !settings.DisabledCategories.Contains(c.Key),
                Id = AchievementCatalog.TryParseCategoryKey(c.Key, out var id) ? id : null,
                AchievementCount = items.Count,
                EnabledCount = items.Count(d => d.Enabled)
            };
        }).ToList();
    }

    /// <summary>
    ///     Maps settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The response.</returns>
    public static AchievementSettingsResponse MapSettings(AchievementGuildSettings settings)
    {
        var row = settings.Row;
        return new AchievementSettingsResponse
        {
            Enabled = row.Enabled,
            AnnounceMode = row.AnnounceMode,
            LogChannelId = row.LogChannelId,
            DmByDefault = row.DmByDefault,
            MentionUsers = row.MentionUsers,
            UnlockMessage = row.UnlockMessage,
            XpPerPoint = row.XpPerPoint,
            RevealHidden = row.RevealHidden,
            UnlockImage = row.UnlockImage,
            DeleteAfter = row.DeleteAfter,
            DisabledCategories = settings.DisabledCategories.ToList(),
            CategoryOrder = settings.CategoryOrder,
            ExcludedRoleIds = settings.ExcludedRoles.ToList(),
            ExcludedChannelIds = settings.ExcludedChannels.ToList(),
            QuietChannelIds = settings.QuietChannels.ToList(),
            RequireSendPermission = row.RequireSendPermission,
            BackfilledAt = row.BackfilledAt
        };
    }

    /// <summary>
    ///     Maps grades.
    /// </summary>
    /// <returns>The responses.</returns>
    public static List<AchievementGradeResponse> MapGrades()
    {
        return AchievementCatalog.Grades.Select(g => new AchievementGradeResponse
        {
            Value = (int)g.Grade,
            Name = g.Name,
            Points = g.Points,
            Color = $"#{g.Color:X6}"
        }).ToList();
    }

    /// <summary>
    ///     Maps a badge.
    /// </summary>
    /// <param name="badge">The badge.</param>
    /// <param name="catalog">The server's catalog, for icon images.</param>
    /// <returns>The response.</returns>
    public static AchievementBadgeResponse MapBadge(AchievementBadge badge, AchievementGuildCatalog catalog)
    {
        return new AchievementBadgeResponse
        {
            Key = badge.Key,
            Name = badge.Name,
            Icon = badge.Icon,
            IconUrl = catalog.ImageUrl(AchievementIcons.Parse(badge.Icon)),
            Grade = (int)badge.Grade,
            Source = badge.Source,
            Short = badge.Short
        };
    }

    /// <summary>
    ///     Maps a member's totals.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="userId">The member.</param>
    /// <param name="points">Points.</param>
    /// <param name="unlocked">Achievements unlocked.</param>
    /// <param name="lastUnlockAt">Latest unlock.</param>
    /// <param name="rank">Leaderboard place.</param>
    /// <param name="client">The Discord client, for members who left.</param>
    /// <returns>The response.</returns>
    public static AchievementMemberResponse MapMember(SocketGuild guild, ulong userId, int points, int unlocked,
        DateTime? lastUnlockAt, int rank, DiscordShardedClient client)
    {
        var member = guild.GetUser(userId);
        IUser? user = member ?? client.GetUser(userId);
        var tier = AchievementCatalog.GetTier(points);
        return new AchievementMemberResponse
        {
            UserId = userId,
            Username = user?.Username ?? userId.ToString(),
            GlobalName = user?.GlobalName,
            DisplayName = member?.DisplayName ?? user?.GlobalName ?? user?.Username ?? userId.ToString(),
            AvatarUrl = member?.GetDisplayAvatarUrl() ?? user?.GetAvatarUrl() ?? user?.GetDefaultAvatarUrl(),
            Points = points,
            Unlocked = unlocked,
            Tier = tier.Name,
            TierGrade = tier.Grade is { } grade ? (int)grade : null,
            LastUnlockAt = lastUnlockAt,
            Rank = rank,
            InServer = member is not null
        };
    }

    /// <summary>
    ///     Maps the whole catalog with reference data.
    /// </summary>
    /// <param name="service">The achievement service.</param>
    /// <param name="guild">The guild.</param>
    /// <returns>The response.</returns>
    public static async Task<AchievementCatalogResponse> MapCatalogAsync(AchievementService service, SocketGuild guild)
    {
        var catalog = await service.GetCatalogAsync(guild.Id);
        var counts = await service.GetUnlockCountsAsync(guild.Id);
        var (overrides, customs) = await service.GetRawRowsAsync(guild.Id);
        var settings = service.GetSettings(guild.Id);

        return new AchievementCatalogResponse
        {
            Categories = MapCategories(catalog, settings),
            Achievements = catalog.All.Select(d => Map(d, catalog, guild, counts, overrides, customs)).ToList(),
            Uploads = catalog.UploadUrls.Where(u => !catalog.CardUploads.Contains(u.Key)).OrderBy(u => u.Key)
                .Select(u => new AchievementIconUploadResponse
            {
                Id = u.Key,
                Icon = AchievementIcons.UploadPrefix + u.Key,
                Url = u.Value
            }).ToList(),
            Grades = MapGrades(),
            Metrics = AchievementCatalog.Metrics.Select(m => new AchievementMetricResponse
            {
                Value = (int)m.Metric,
                Key = m.Metric.ToString(),
                Label = m.Label,
                Unit = m.Unit,
                UnitPlural = m.UnitPlural,
                Description = m.Description,
                Source = m.Source,
                AllowCustom = m.AllowCustom
            }).ToList(),
            Tiers = AchievementCatalog.Tiers.Select(t => new AchievementTierResponse
            {
                Name = t.Name,
                MinPoints = t.MinPoints,
                Grade = t.Grade is { } grade ? (int)grade : null
            }).ToList(),
            Placeholders = AchievementService.UnlockPlaceholders.Select(p => new AchievementPlaceholderResponse
            {
                Name = p.Name,
                Description = p.Description
            }).ToList(),
            Limits = new AchievementLimitsResponse
            {
                MaxCustomAchievements = AchievementService.MaxCustomAchievements,
                MaxCustomCategories = AchievementService.MaxCustomCategories,
                NameLength = AchievementService.NameLength,
                DescriptionLength = AchievementService.DescriptionLength,
                KeywordLength = AchievementService.KeywordLength,
                MessageLength = AchievementService.MessageLength,
                MaxPoints = AchievementService.MaxPoints,
                MaxReward = AchievementService.MaxReward,
                BadgeSlots = AchievementCatalog.BadgeSlots
            }
        };
    }

    /// <summary>
    ///     A plain text message for a refused write.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The message.</returns>
    public static string Describe(AchievementError error)
    {
        return error switch
        {
            AchievementError.NotFound => "That achievement or category doesn't exist.",
            AchievementError.NameInvalid => $"Names have to be 1 to {AchievementService.NameLength} characters.",
            AchievementError.TextTooLong => "One of the fields is too long.",
            AchievementError.MetricInvalid => "That isn't something server achievements can count.",
            AchievementError.ThresholdInvalid => "The goal has to be at least 1.",
            AchievementError.KeywordMissing => "Add a phrase or emoji to watch for.",
            AchievementError.TooManyAchievements =>
                $"This server already has {AchievementService.MaxCustomAchievements} achievements.",
            AchievementError.TooManyCategories =>
                $"This server already has {AchievementService.MaxCustomCategories} categories.",
            AchievementError.RoleNotAssignable =>
                "The bot can't give out that role. Move the bot's highest role above it, and make sure no integration manages it.",
            AchievementError.CategoryInvalid => "That category doesn't exist.",
            AchievementError.IconInvalid =>
                "Pick an icon from the list, a server emoji, an https image link, or a PNG, JPEG, GIF, or WebP under 4 MB.",
            AchievementError.TooManyUploads =>
                $"This server already has {AchievementIconService.MaxUploads} uploaded icons. Delete one first.",
            AchievementError.TooManyCards =>
                $"This server already has {AchievementService.MaxCardDesigns} card designs. Delete one first.",
            AchievementError.TooManyCardImages =>
                $"This server already has {AchievementIconService.MaxCardUploads} card images. Delete one first.",
            AchievementError.RewardInvalid => "Points and rewards have to be zero or more, and within limits.",
            AchievementError.AlreadyInState => "Nothing to change.",
            AchievementError.BadgeInvalid => "That badge isn't owned, or that slot doesn't exist.",
            _ => "Something went wrong saving that."
        };
    }
}
