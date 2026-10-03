using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Achievements.Common;

namespace Mewdeko.Modules.Achievements.Services;

public sealed partial class AchievementService
{
    #region Members

    /// <summary>
    ///     A member's unlocked achievements.
    /// </summary>
    /// <param name="guildId">The guild ID, or 0 for global achievements.</param>
    /// <param name="userId">The member.</param>
    /// <returns>Key to unlock time.</returns>
    public async Task<Dictionary<string, DateTime>> GetUnlocksAsync(ulong guildId, ulong userId)
    {
        var state = await GetUnlockStateAsync(guildId, userId);
        return state.Snapshot();
    }

    /// <summary>
    ///     A member's totals, rank, and equipped badges.
    /// </summary>
    /// <param name="guildId">The guild ID, or 0 for global totals.</param>
    /// <param name="userId">The member.</param>
    /// <returns>The summary.</returns>
    public async Task<AchievementMemberSummary> GetSummaryAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.AchievementMembers.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
        var points = row?.Points ?? 0;
        var hidden = await GetHiddenFromLeaderboardsAsync(db);
        var rank = row is null || points == 0
            ? 0
            : await db.AchievementMembers.CountAsync(x =>
                x.GuildId == guildId && x.Points > points && !hidden.Contains(x.UserId)) + 1;

        var total = guildId == AchievementCatalog.GlobalGuildId
            ? AchievementCatalog.BuiltIns.Count(d => d.IsGlobal)
            : (await GetCatalogAsync(guildId)).Earnable.Count;

        return new AchievementMemberSummary
        {
            UserId = userId,
            Points = points,
            Unlocked = row?.UnlockedCount ?? 0,
            Total = total,
            Rank = rank,
            Tier = AchievementCatalog.GetTier(points),
            NextTier = AchievementCatalog.GetNextTier(points),
            LastUnlockAt = row?.LastUnlockAt,
            Equipped = [row?.Badge1, row?.Badge2, row?.Badge3, row?.Badge4]
        };
    }

    /// <summary>
    ///     A member's global totals: points, unlocks, and servers across every server.
    /// </summary>
    /// <param name="userId">The member.</param>
    /// <returns>Points, achievements, and servers.</returns>
    public async Task<(long Points, long Unlocked, int Servers)> GetGlobalTotalsAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var rows = await db.AchievementMembers
            .Where(x => x.UserId == userId && x.GuildId != AchievementCatalog.GlobalGuildId)
            .Select(x => new
            {
                x.Points, x.UnlockedCount
            })
            .ToListAsync();
        return (rows.Sum(r => (long)r.Points), rows.Sum(r => (long)r.UnlockedCount), rows.Count(r => r.UnlockedCount > 0));
    }

    /// <summary>
    ///     Where a member stands on achievements, with current values for metric ones.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="userId">The member.</param>
    /// <param name="filter">Which achievements to include, or null for all the server shows.</param>
    /// <returns>Progress in display order.</returns>
    public async Task<List<AchievementProgress>> GetProgressAsync(SocketGuild guild, ulong userId,
        Func<AchievementDefinition, bool>? filter = null)
    {
        var catalog = await GetCatalogAsync(guild.Id);
        var unlocks = await GetUnlocksAsync(guild.Id, userId);
        var shown = catalog.All
            .Where(d => !d.IsGlobal && (d.Enabled || unlocks.ContainsKey(d.Key)))
            .Where(d => filter?.Invoke(d) ?? true)
            .ToList();

        var metrics = shown
            .Where(d => d.IsMetric && !unlocks.ContainsKey(d.Key))
            .Select(d => d.Metric)
            .ToHashSet();
        var values = metrics.Count > 0 && guild.GetUser(userId) is not null
            ? (await ComputeMetricsAsync(guild, [userId], metrics)).GetValueOrDefault(userId)
            : null;

        return shown.Select(d => new AchievementProgress
        {
            Definition = d,
            UnlockedAt = unlocks.TryGetValue(d.Key, out var at) ? at : null,
            Current = d.IsMetric && values is not null && values.TryGetValue(d.Metric, out var value) ? value : null
        }).ToList();
    }

    /// <summary>
    ///     A member's global achievements and where they stand on each.
    /// </summary>
    /// <param name="userId">The member.</param>
    /// <returns>Progress in display order.</returns>
    public async Task<List<AchievementProgress>> GetGlobalProgressAsync(ulong userId)
    {
        var unlocks = await GetUnlocksAsync(AchievementCatalog.GlobalGuildId, userId);
        var (points, unlocked, servers) = await GetGlobalTotalsAsync(userId);
        return AchievementCatalog.BuiltIns
            .Where(d => d.IsGlobal)
            .Select(d => new AchievementProgress
            {
                Definition = d,
                UnlockedAt = unlocks.TryGetValue(d.Key, out var at) ? at : null,
                Current = d.Metric switch
                {
                    AchievementMetric.GlobalPoints => points,
                    AchievementMetric.GlobalUnlocks => unlocked,
                    AchievementMetric.GlobalServers => servers,
                    _ => null
                }
            })
            .ToList();
    }

    /// <summary>
    ///     How many members unlocked each achievement in a server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Key to unlock count.</returns>
    public async Task<Dictionary<string, int>> GetUnlockCountsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.UserAchievements
            .Where(x => x.GuildId == guildId)
            .GroupBy(x => x.AchievementKey)
            .Select(g => new KeyCount
            {
                Key = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    /// <summary>
    ///     Server wide totals for the dashboard.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Members with an unlock, total unlocks, and unlocks in the last week.</returns>
    public async Task<(int Members, int Unlocks, int ThisWeek)> GetGuildTotalsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var members = await db.AchievementMembers.CountAsync(x => x.GuildId == guildId && x.UnlockedCount > 0);
        var unlocks = await db.UserAchievements.CountAsync(x => x.GuildId == guildId);
        var since = DateTime.UtcNow.AddDays(-7);
        var week = await db.UserAchievements.CountAsync(x => x.GuildId == guildId && x.UnlockedAt >= since);
        return (members, unlocks, week);
    }

    /// <summary>
    ///     Recent unlocks in a server, newest first.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="count">How many.</param>
    /// <returns>The unlocks.</returns>
    public async Task<List<UserAchievement>> GetRecentUnlocksAsync(ulong guildId, int count)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.UserAchievements
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.UnlockedAt)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    ///     A page of the leaderboard, skipping members who hide themselves.
    /// </summary>
    /// <param name="guildId">The guild ID, or 0 for the global leaderboard.</param>
    /// <param name="sort">Ordering.</param>
    /// <param name="page">0 based page.</param>
    /// <param name="pageSize">Rows per page.</param>
    /// <param name="includeHidden">Whether to include members who hide themselves, for staff views.</param>
    /// <returns>The rows and the total row count.</returns>
    public async Task<(List<AchievementLeaderboardEntry> Entries, int Total)> GetLeaderboardAsync(ulong guildId,
        AchievementLeaderboardSort sort, int page, int pageSize, bool includeHidden = false)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var hidden = includeHidden ? new List<ulong>() : await GetHiddenFromLeaderboardsAsync(db);

        if (guildId == AchievementCatalog.GlobalGuildId)
        {
            var global = db.AchievementMembers
                .Where(x => x.GuildId != AchievementCatalog.GlobalGuildId && x.UnlockedCount > 0 &&
                            !hidden.Contains(x.UserId))
                .GroupBy(x => x.UserId)
                .Select(g => new GlobalAggregate
                {
                    UserId = g.Key,
                    Points = g.Sum(x => x.Points),
                    Unlocked = g.Sum(x => x.UnlockedCount),
                    LastUnlockAt = g.Max(x => x.LastUnlockAt)
                });
            var globalTotal = await global.CountAsync();
            var ordered = sort switch
            {
                AchievementLeaderboardSort.Unlocked => global.OrderByDescending(x => x.Unlocked).ThenByDescending(x => x.Points),
                AchievementLeaderboardSort.Recent => global.OrderByDescending(x => x.LastUnlockAt),
                _ => global.OrderByDescending(x => x.Points).ThenByDescending(x => x.Unlocked)
            };
            var globalRows = await ordered.Skip(page * pageSize).Take(pageSize).ToListAsync();
            return (globalRows.Select((r, i) => new AchievementLeaderboardEntry
            {
                Rank = page * pageSize + i + 1,
                UserId = r.UserId,
                Points = r.Points,
                Unlocked = r.Unlocked,
                LastUnlockAt = r.LastUnlockAt
            }).ToList(), globalTotal);
        }

        var query = db.AchievementMembers
            .Where(x => x.GuildId == guildId && x.UnlockedCount > 0 && !hidden.Contains(x.UserId));
        var total = await query.CountAsync();
        var sorted = sort switch
        {
            AchievementLeaderboardSort.Unlocked => query.OrderByDescending(x => x.UnlockedCount).ThenByDescending(x => x.Points),
            AchievementLeaderboardSort.Recent => query.OrderByDescending(x => x.LastUnlockAt),
            _ => query.OrderByDescending(x => x.Points).ThenByDescending(x => x.UnlockedCount)
        };
        var rows = await sorted.Skip(page * pageSize).Take(pageSize).ToListAsync();
        return (rows.Select((r, i) => new AchievementLeaderboardEntry
        {
            Rank = page * pageSize + i + 1,
            UserId = r.UserId,
            Points = r.Points,
            Unlocked = r.UnlockedCount,
            LastUnlockAt = r.LastUnlockAt
        }).ToList(), total);
    }

    /// <summary>
    ///     Totals for specific members, for the dashboard member list.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userIds">The members.</param>
    /// <returns>Rows by member.</returns>
    public async Task<Dictionary<ulong, AchievementMember>> GetMemberRowsAsync(ulong guildId,
        IReadOnlyCollection<ulong> userIds)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.AchievementMembers
            .Where(x => x.GuildId == guildId && userIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId);
    }

    /// <summary>
    ///     The stored overrides and server made achievements, for editors that need raw values.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Overrides by key and server made achievements by ID.</returns>
    public async Task<(Dictionary<string, AchievementOverride> Overrides, Dictionary<int, CustomAchievement> Customs)>
        GetRawRowsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var overrides = await db.AchievementOverrides.Where(x => x.GuildId == guildId)
            .ToDictionaryAsync(x => x.AchievementKey, StringComparer.Ordinal);
        var customs = await db.CustomAchievements.Where(x => x.GuildId == guildId).ToDictionaryAsync(x => x.Id);
        return (overrides, customs);
    }

    private static async Task<List<ulong>> GetHiddenFromLeaderboardsAsync(Database.DbContextStuff.MewdekoDb db)
    {
        return await db.AchievementUserSettings.Where(x => x.HideFromLeaderboards).Select(x => x.UserId).ToListAsync();
    }

    #endregion

    #region Badges

    /// <summary>
    ///     Every badge a server offers.
    /// </summary>
    /// <param name="catalog">The server's catalog.</param>
    /// <returns>The badges.</returns>
    public static List<AchievementBadge> AllBadges(AchievementGuildCatalog catalog)
    {
        var badges = new List<AchievementBadge>();
        foreach (var category in catalog.Categories.Where(c => c.HasBadges))
        {
            var grades = catalog.All
                .Where(d => d.CategoryKey == category.Key && !d.IsCustom)
                .Select(d => d.Grade)
                .Distinct()
                .OrderBy(g => g);
            badges.AddRange(grades.Select(g => CategoryBadge(category, g)));
        }

        badges.AddRange(AchievementCatalog.Tiers.Where(t => t.Grade is not null).Select(t => TierBadge(t.Grade!.Value)));
        badges.AddRange(catalog.All.Where(d => d.IsCustom).Select(d => CustomBadge(d, catalog)));
        return badges;
    }

    /// <summary>
    ///     Looks up a badge by key.
    /// </summary>
    /// <param name="catalog">The server's catalog.</param>
    /// <param name="key">The badge key.</param>
    /// <returns>The badge, or null.</returns>
    public static AchievementBadge? FindBadge(AchievementGuildCatalog catalog, string key)
    {
        return AllBadges(catalog).FirstOrDefault(b => b.Key == key);
    }

    /// <summary>
    ///     Badges a member owns in a server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>Owned badges, best first.</returns>
    public async Task<List<AchievementBadge>> GetOwnedBadgesAsync(ulong guildId, ulong userId)
    {
        var catalog = await GetCatalogAsync(guildId);
        var unlocks = await GetUnlocksAsync(guildId, userId);
        var summary = await GetSummaryAsync(guildId, userId);
        var owned = new List<AchievementBadge>();

        var unlockedDefs = unlocks.Keys
            .Select(k => catalog.ByKey.GetValueOrDefault(k))
            .Where(d => d is not null)
            .Cast<AchievementDefinition>()
            .ToList();

        foreach (var group in unlockedDefs.Where(d => !d.IsCustom).GroupBy(d => d.CategoryKey))
        {
            var category = catalog.Category(group.Key);
            if (!category.HasBadges)
                continue;
            owned.AddRange(group.Select(d => d.Grade).Distinct().Select(g => CategoryBadge(category, g)));
        }

        owned.AddRange(AchievementCatalog.Tiers
            .Where(t => t.Grade is not null && summary.Points >= t.MinPoints)
            .Select(t => TierBadge(t.Grade!.Value)));
        owned.AddRange(unlockedDefs.Where(d => d.IsCustom).Select(d => CustomBadge(d, catalog)));

        return owned.OrderByDescending(b => b.Grade).ThenBy(b => b.Name).ToList();
    }

    /// <summary>
    ///     Puts a badge in a profile slot.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="slot">1 to 4.</param>
    /// <param name="badgeKey">The badge key.</param>
    /// <returns>The badge.</returns>
    public async Task<AchievementResult<AchievementBadge>> EquipBadgeAsync(ulong guildId, ulong userId, int slot,
        string badgeKey)
    {
        if (slot is < 1 or > AchievementCatalog.BadgeSlots)
            return AchievementResult<AchievementBadge>.Fail(AchievementError.BadgeInvalid);

        var owned = await GetOwnedBadgesAsync(guildId, userId);
        var badge = owned.FirstOrDefault(b => b.Key == badgeKey);
        if (badge is null)
            return AchievementResult<AchievementBadge>.Fail(AchievementError.BadgeInvalid);

        await SetSlotsAsync(guildId, userId, slots =>
        {
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == badgeKey)
                    slots[i] = null;
            }

            slots[slot - 1] = badgeKey;
        });
        return AchievementResult<AchievementBadge>.Ok(badge);
    }

    /// <summary>
    ///     Empties a profile slot.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="slot">1 to 4.</param>
    /// <returns>Whether the slot was valid.</returns>
    public async Task<bool> UnequipBadgeAsync(ulong guildId, ulong userId, int slot)
    {
        if (slot is < 1 or > AchievementCatalog.BadgeSlots)
            return false;
        await SetSlotsAsync(guildId, userId, slots => slots[slot - 1] = null);
        return true;
    }

    /// <summary>
    ///     Replaces every profile slot at once. Badges the member doesn't own are dropped.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="keys">Badge keys for slots 1 to 4, null for empty.</param>
    /// <returns>The saved slots.</returns>
    public async Task<string?[]> SetBadgeSlotsAsync(ulong guildId, ulong userId, IReadOnlyList<string?> keys)
    {
        var owned = (await GetOwnedBadgesAsync(guildId, userId)).Select(b => b.Key).ToHashSet(StringComparer.Ordinal);
        var cleaned = new string?[AchievementCatalog.BadgeSlots];
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < cleaned.Length && i < keys.Count; i++)
        {
            var key = keys[i];
            if (key is not null && owned.Contains(key) && used.Add(key))
                cleaned[i] = key;
        }

        await SetSlotsAsync(guildId, userId, slots => Array.Copy(cleaned, slots, slots.Length));
        return cleaned;
    }

    private async Task SetSlotsAsync(ulong guildId, ulong userId, Action<string?[]> change)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.AchievementMembers.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
        var slots = new[]
        {
            row?.Badge1, row?.Badge2, row?.Badge3, row?.Badge4
        };
        change(slots);

        if (row is null)
        {
            await db.InsertAsync(new AchievementMember
            {
                GuildId = guildId,
                UserId = userId,
                Badge1 = slots[0],
                Badge2 = slots[1],
                Badge3 = slots[2],
                Badge4 = slots[3],
                DateAdded = DateTime.UtcNow
            });
            return;
        }

        await db.AchievementMembers
            .Where(x => x.GuildId == guildId && x.UserId == userId)
            .Set(x => x.Badge1, slots[0])
            .Set(x => x.Badge2, slots[1])
            .Set(x => x.Badge3, slots[2])
            .Set(x => x.Badge4, slots[3])
            .UpdateAsync();
    }

    private static AchievementBadge CategoryBadge(AchievementCategoryInfo category, AchievementGrade grade)
    {
        var gradeInfo = AchievementCatalog.GetGrade(grade);
        return new AchievementBadge
        {
            Key = $"{category.Key}:{gradeInfo.Name.ToLowerInvariant()}",
            Name = $"{gradeInfo.Name} {category.Name}",
            Icon = category.Icon,
            Grade = grade,
            Source = $"Unlock a {gradeInfo.Name} {category.Name} achievement",
            Short = ShortLabel(category.Name)
        };
    }

    private static AchievementBadge TierBadge(AchievementGrade grade)
    {
        var gradeInfo = AchievementCatalog.GetGrade(grade);
        var tier = AchievementCatalog.Tiers.First(t => t.Grade == grade);
        return new AchievementBadge
        {
            Key = $"tier:{gradeInfo.Name.ToLowerInvariant()}",
            Name = $"{tier.Name} Rank",
            Icon = AchievementIcons.GlyphPrefix + AchievementIcons.GradeGlyph,
            Grade = grade,
            Source = $"Reach {tier.MinPoints:N0} points",
            Short = gradeInfo.Name[..1]
        };
    }

    private static AchievementBadge CustomBadge(AchievementDefinition def, AchievementGuildCatalog catalog)
    {
        return new AchievementBadge
        {
            Key = def.Key,
            Name = def.Name,
            Icon = catalog.IconFor(def).Stored,
            Grade = def.Grade,
            Source = $"Unlock {def.Name}",
            Short = ShortLabel(def.Name)
        };
    }

    private static string ShortLabel(string name)
    {
        var letters = new string(name.Where(char.IsLetterOrDigit).ToArray());
        return letters.Length == 0 ? "?" : letters[..Math.Min(3, letters.Length)].ToUpperInvariant();
    }

    #endregion

    #region User settings

    /// <summary>
    ///     A user's achievement privacy and notification choices.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns>The settings, or defaults.</returns>
    public async Task<AchievementUserSetting> GetUserSettingsAsync(ulong userId)
    {
        if (userSettingsCache.TryGetValue(userId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.AchievementUserSettings.FirstOrDefaultAsync(x => x.UserId == userId)
                  ?? new AchievementUserSetting
                  {
                      UserId = userId
                  };
        userSettingsCache[userId] = row;
        return row;
    }

    /// <summary>
    ///     Changes a user's achievement privacy and notification choices.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="change">Applies the change.</param>
    /// <returns>The saved settings.</returns>
    public async Task<AchievementUserSetting> UpdateUserSettingsAsync(ulong userId, Action<AchievementUserSetting> change)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.AchievementUserSettings.FirstOrDefaultAsync(x => x.UserId == userId);
        if (row is null)
        {
            row = new AchievementUserSetting
            {
                UserId = userId,
                DateAdded = DateTime.UtcNow
            };
            change(row);
            await db.InsertAsync(row);
        }
        else
        {
            change(row);
            await db.UpdateAsync(row);
        }

        userSettingsCache[userId] = row;
        return row;
    }

    /// <summary>
    ///     Sets who can see part of a user's achievements.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="area">What the setting covers.</param>
    /// <param name="visibility">Who can see it.</param>
    /// <returns>The saved settings.</returns>
    public Task<AchievementUserSetting> SetPrivacyAsync(ulong userId, AchievementPrivacyArea area,
        AchievementVisibility visibility)
    {
        return UpdateUserSettingsAsync(userId, row =>
        {
            switch (area)
            {
                case AchievementPrivacyArea.Profile:
                    row.ProfileVisibility = (int)visibility;
                    break;
                case AchievementPrivacyArea.Achievements:
                    row.AchievementsVisibility = (int)visibility;
                    break;
                case AchievementPrivacyArea.Badges:
                    row.BadgesVisibility = (int)visibility;
                    break;
                case AchievementPrivacyArea.Leaderboard:
                    row.HideFromLeaderboards = visibility == AchievementVisibility.OnlyMe;
                    break;
            }
        });
    }

    /// <summary>
    ///     Whether someone may see part of another member's achievements.
    /// </summary>
    /// <param name="viewerId">Who is looking.</param>
    /// <param name="targetId">Whose achievements.</param>
    /// <param name="area">What they are looking at.</param>
    /// <returns>True when allowed.</returns>
    public async Task<bool> CanViewAsync(ulong viewerId, ulong targetId, AchievementPrivacyArea area)
    {
        if (viewerId == targetId)
            return true;

        var settings = await GetUserSettingsAsync(targetId);
        return area switch
        {
            AchievementPrivacyArea.Profile => settings.ProfileVisibility == (int)AchievementVisibility.Everyone,
            AchievementPrivacyArea.Achievements => settings.AchievementsVisibility == (int)AchievementVisibility.Everyone,
            AchievementPrivacyArea.Badges => settings.BadgesVisibility == (int)AchievementVisibility.Everyone,
            AchievementPrivacyArea.Leaderboard => !settings.HideFromLeaderboards,
            _ => true
        };
    }

    #endregion

    /// <summary>
    ///     An unlock count for one key.
    /// </summary>
    private sealed class KeyCount
    {
        /// <summary>
        ///     The achievement key.
        /// </summary>
        public string Key { get; set; } = "";

        /// <summary>
        ///     How many members unlocked it.
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    ///     A member's totals across every server.
    /// </summary>
    private sealed class GlobalAggregate
    {
        /// <summary>
        ///     The member.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        ///     Points across every server.
        /// </summary>
        public int Points { get; set; }

        /// <summary>
        ///     Achievements across every server.
        /// </summary>
        public int Unlocked { get; set; }

        /// <summary>
        ///     Most recent unlock anywhere.
        /// </summary>
        public DateTime? LastUnlockAt { get; set; }
    }
}
