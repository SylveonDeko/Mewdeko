using System.Text.Json;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Cards;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Achievements.Services;
using Mewdeko.Modules.CardDesigns.Common;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Services;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Modules.CardDesigns.Services;

/// <summary>
///     Why a member's save was refused.
/// </summary>
public enum MemberSaveError
{
    /// <summary>
    ///     Nothing went wrong.
    /// </summary>
    None,

    /// <summary>
    ///     The community doesn't let members make designs.
    /// </summary>
    Disabled,

    /// <summary>
    ///     The member doesn't meet the community's requirement.
    /// </summary>
    NotEligible,

    /// <summary>
    ///     The community only allows presets.
    /// </summary>
    UploadsOff,

    /// <summary>
    ///     The design data isn't acceptable.
    /// </summary>
    BadData,

    /// <summary>
    ///     The image couldn't be used.
    /// </summary>
    BadImage,

    /// <summary>
    ///     The community's member upload space is full.
    /// </summary>
    PoolFull,

    /// <summary>
    ///     The member can't pay the cost.
    /// </summary>
    NoFunds
}

/// <summary>
///     The outcome of a member saving their design.
/// </summary>
/// <param name="Design">The stored design, when it worked.</param>
/// <param name="Error">Why it didn't.</param>
/// <param name="Message">A sentence describing a data or image problem.</param>
/// <param name="ImageError">The upload error, for image problems.</param>
public sealed record MemberSaveResult(CardDesign? Design, MemberSaveError Error, string? Message = null,
    AchievementError? ImageError = null);

/// <summary>
///     The outcome of a moderator acting on a member design.
/// </summary>
public enum MemberModerationOutcome
{
    /// <summary>
    ///     It was done.
    /// </summary>
    Done,

    /// <summary>
    ///     The design doesn't exist.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The design has no upload waiting for review.
    /// </summary>
    NothingPending
}

/// <summary>
///     The result of a moderation action.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Notified">Whether the member's DM went through. Always false for approvals, which send none.</param>
public sealed record MemberModerationResult(MemberModerationOutcome Outcome, bool Notified = false);

public sealed partial class CardDesignService
{
    #region Settings

    /// <summary>
    ///     Gets a community's member design rules, or the defaults when it never set any.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The rules.</returns>
    public async Task<CardDesignSetting> GetSettingsAsync(ulong guildId)
    {
        if (settingsCache.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.CardDesignSettings.FirstOrDefaultAsync(x => x.GuildId == guildId) ??
                  new CardDesignSetting
                  {
                      GuildId = guildId
                  };
        settingsCache[guildId] = row;
        return row;
    }

    /// <summary>
    ///     Changes a community's member design rules.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="mutate">An action applying the changes.</param>
    /// <returns>The stored rules.</returns>
    public async Task<CardDesignSetting> SaveSettingsAsync(ulong guildId, Action<CardDesignSetting> mutate)
    {
        var current = await GetSettingsAsync(guildId);
        var row = new CardDesignSetting
        {
            GuildId = guildId,
            MemberEnabled = current.MemberEnabled,
            MemberWho = current.MemberWho,
            MemberRoleIds = current.MemberRoleIds,
            MemberMinLevel = current.MemberMinLevel,
            MemberUploads = current.MemberUploads,
            MemberReview = current.MemberReview,
            MemberCost = current.MemberCost
        };
        mutate(row);

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.InsertOrReplaceAsync(row);
        settingsCache[guildId] = row;
        return row;
    }

    /// <summary>
    ///     Works out whether a member may make a design right now.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>Whether they may, or what they are missing.</returns>
    public async Task<MemberEligibility> GetEligibilityAsync(ulong guildId, ulong userId)
    {
        var settings = await GetSettingsAsync(guildId);
        if (!settings.MemberEnabled)
            return MemberEligibility.Disabled;

        var who = (MemberDesignWho)settings.MemberWho;
        IReadOnlyCollection<ulong> roles = [];
        var level = 0;
        if (who == MemberDesignWho.Roles)
        {
            var user = client.GetGuild(guildId)?.GetUser(userId);
            roles = user?.Roles.Select(r => r.Id).ToList() ?? [];
        }
        else if (who == MemberDesignWho.Level)
        {
            level = await services.GetRequiredService<XpService>().GetUserLevelAsync(guildId, userId);
        }

        return CardDesignRules.CheckMember(settings, level, roles);
    }

    /// <summary>
    ///     Whether a member design still renders: the community allows member designs and the creator still qualifies.
    /// </summary>
    /// <param name="design">The member design.</param>
    /// <returns>True when it can be drawn.</returns>
    public async Task<bool> IsMemberUsableAsync(CardDesign design)
    {
        return design.OwnerUserId is { } owner &&
               await GetEligibilityAsync(design.GuildId, owner) == MemberEligibility.Eligible;
    }

    #endregion

    #region Member designs

    /// <summary>
    ///     Gets every member design in a guild, oldest first.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The member designs.</returns>
    public async Task<IReadOnlyList<CardDesign>> GetMemberDesignsAsync(ulong guildId)
    {
        if (memberCache.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var designs = await db.CardDesigns.Where(x => x.GuildId == guildId && x.OwnerUserId != null)
            .OrderBy(x => x.Id)
            .ToListAsync();
        memberCache[guildId] = designs;
        return designs;
    }

    /// <summary>
    ///     Gets a member design by ID.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <returns>The design, or null.</returns>
    public async Task<CardDesign?> GetMemberDesignByIdAsync(ulong guildId, int id)
    {
        return (await GetMemberDesignsAsync(guildId)).FirstOrDefault(d => d.Id == id);
    }

    /// <summary>
    ///     Gets a member's design for one card.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="target">The card.</param>
    /// <returns>The design, or null.</returns>
    public async Task<CardDesign?> GetMemberDesignAsync(ulong guildId, ulong userId, CardDesignTarget target)
    {
        return (await GetMemberDesignsAsync(guildId))
            .FirstOrDefault(d => d.OwnerUserId == userId && d.Target == (short)target);
    }

    /// <summary>
    ///     Checks and cleans the data a member sent for their design. Members only get presets, colours and layout.
    /// </summary>
    /// <param name="target">The card.</param>
    /// <param name="raw">The data as sent, or null for an empty design.</param>
    /// <returns>The data to store, or what is wrong with it.</returns>
    public async Task<(string? Data, string? Error)> NormalizeMemberDataAsync(CardDesignTarget target,
        JsonElement? raw)
    {
        var presets = services.GetRequiredService<CardPresetService>();
        if (target == CardDesignTarget.Rank)
        {
            string? reference = null;
            if (raw is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("background", out var property))
            {
                if (property.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                    return (null, "Background must be text.");
                reference = property.GetString()?.Trim();
            }

            if (!string.IsNullOrEmpty(reference) &&
                (!CardPresets.TryParse(reference, out _) || !await presets.ExistsAsync(reference)))
                return (null, "Pick a preset background.");

            return (CardDesignRules.RankData(reference), null);
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

        template = AchievementCards.Sanitize(template);
        if (CardDesignRules.MemberTemplateProblem(template) is { } problem)
            return (null, problem);

        foreach (var url in template.Elements.Select(e => e.Url).Append(template.Background.Url)
                     .Where(u => !string.IsNullOrWhiteSpace(u)).Distinct())
        {
            if (!await presets.ExistsAsync(url))
                return (null, "That preset doesn't exist.");
        }

        return (template.Serialize(), null);
    }

    /// <summary>
    ///     Creates or replaces a member's design for one card. Replacing never makes a second copy.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="target">The card.</param>
    /// <param name="raw">The design data, or null to keep the current one.</param>
    /// <param name="upload">A new background image, or null.</param>
    /// <param name="removeUpload">Whether to drop the design's uploaded background.</param>
    /// <returns>The stored design, or why it was refused.</returns>
    public async Task<MemberSaveResult> SaveMemberAsync(ulong guildId, ulong userId, CardDesignTarget target,
        JsonElement? raw, byte[]? upload, bool removeUpload)
    {
        var settings = await GetSettingsAsync(guildId);
        switch (await GetEligibilityAsync(guildId, userId))
        {
            case MemberEligibility.Disabled:
                return new MemberSaveResult(null, MemberSaveError.Disabled);
            case not MemberEligibility.Eligible:
                return new MemberSaveResult(null, MemberSaveError.NotEligible);
        }

        if (upload is not null && !settings.MemberUploads)
            return new MemberSaveResult(null, MemberSaveError.UploadsOff);

        var existing = await GetMemberDesignAsync(guildId, userId, target);
        string data;
        if (raw is null && existing is not null)
        {
            data = existing.Data;
        }
        else
        {
            var (normalized, problem) = await NormalizeMemberDataAsync(target, raw);
            if (problem is not null)
                return new MemberSaveResult(null, MemberSaveError.BadData, problem);
            data = normalized!;
        }

        var icons = services.GetRequiredService<AchievementIconService>();
        AchievementIconUpload? stored = null;
        if (upload is not null)
        {
            var result = await icons.UploadAsync(guildId, userId, upload, AchievementIconService.MemberCardKind);
            if (!result.Success)
                return new MemberSaveResult(null,
                    result.Error == AchievementError.TooManyMemberImages
                        ? MemberSaveError.PoolFull
                        : MemberSaveError.BadImage, null, result.Error);
            stored = result.Value;
        }

        var currency = services.GetRequiredService<ICurrencyService>();
        var cost = settings.MemberCost;
        if (cost > 0 && !await currency.TryDebitAsync(userId, cost, "Card design: member design",
                CurrencyCategory.ShopPurchase, guildId))
        {
            if (stored is not null)
                await icons.DeleteAsync(guildId, stored.Id);
            return new MemberSaveResult(null, MemberSaveError.NoFunds);
        }

        var oldUpload = existing?.UploadId;
        var keepsUpload = stored is null && !removeUpload && oldUpload is not null;
        var uploadId = stored?.Id ?? (keepsUpload ? oldUpload : null);
        var status = CardDesignRules.NextReview(uploadId is not null, stored is not null, settings.MemberReview,
            (CardDesignReview)(existing?.ReviewStatus ?? 0));

        CardDesign saved;
        try
        {
            saved = await UpsertMemberAsync(guildId, userId, target, data, uploadId, status, existing is not null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not save a member card design for {UserId} in {GuildId}", userId, guildId);
            if (cost > 0)
                await currency.CreditAsync(userId, cost, "Card design: refund", CurrencyCategory.ShopRefund, guildId);
            if (stored is not null)
                await icons.DeleteAsync(guildId, stored.Id);
            throw;
        }

        if (stored is not null && status == CardDesignReview.Approved)
            await icons.PublishApprovedAsync(guildId, stored.Id);
        if (oldUpload is { } old && old != uploadId)
            await icons.DeleteAsync(guildId, old);

        return new MemberSaveResult(saved, MemberSaveError.None);
    }

    private async Task<CardDesign> UpsertMemberAsync(ulong guildId, ulong userId, CardDesignTarget target,
        string data, int? uploadId, CardDesignReview status, bool exists)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var now = DateTime.UtcNow;
        if (exists)
        {
            await UpdateMemberAsync(db, guildId, userId, target, data, uploadId, status, now);
        }
        else
        {
            try
            {
                await db.InsertAsync(new CardDesign
                {
                    GuildId = guildId,
                    Name = CardDesignRules.MemberName(userId, target),
                    Target = (short)target,
                    Data = data,
                    Availability = (short)CardDesignAvailability.Free,
                    OwnerUserId = userId,
                    ReviewStatus = (short)status,
                    UploadId = uploadId,
                    DateAdded = now,
                    DateUpdated = now
                });
            }
            catch (Exception ex) when (IsDuplicate(ex))
            {
                await UpdateMemberAsync(db, guildId, userId, target, data, uploadId, status, now);
            }
        }

        memberCache.TryRemove(guildId, out _);
        return (await GetMemberDesignAsync(guildId, userId, target))!;
    }

    private static Task<int> UpdateMemberAsync(MewdekoDb db, ulong guildId, ulong userId,
        CardDesignTarget target, string data, int? uploadId, CardDesignReview status, DateTime now)
    {
        return db.CardDesigns.Where(x => x.GuildId == guildId && x.OwnerUserId == userId && x.Target == (short)target)
            .Set(x => x.Data, data)
            .Set(x => x.UploadId, uploadId)
            .Set(x => x.ReviewStatus, (short)status)
            .Set(x => x.ReviewNote, (string?)null)
            .Set(x => x.DateUpdated, now)
            .UpdateAsync();
    }

    /// <summary>
    ///     Reads a member design's stored upload as a data address, for its creator and moderators to preview.
    /// </summary>
    /// <param name="design">The member design.</param>
    /// <returns>The data address, or null when it has no upload.</returns>
    public async Task<string?> GetMemberImageAsync(CardDesign design)
    {
        if (design.UploadId is not { } id)
            return null;

        var bytes = await services.GetRequiredService<AchievementIconService>().GetBytesAsync(design.GuildId, id);
        return bytes is null ? null : "data:image/png;base64," + Convert.ToBase64String(bytes);
    }

    /// <summary>
    ///     Resolves a preset background of a member design to an address a browser can load.
    /// </summary>
    /// <param name="design">The member design.</param>
    /// <returns>The address, or null.</returns>
    public async Task<string?> GetMemberPresetUrlAsync(CardDesign design)
    {
        var reference = CardDesignRules.BackgroundRef((CardDesignTarget)design.Target, design.Data);
        if (reference is null || !CardPresets.TryParse(reference, out var presetId))
            return null;

        var presets = services.GetRequiredService<CardPresetService>();
        var preset = await presets.FindAsync(presetId);
        return preset is null ? null : CardPresets.Url(presets.BaseUrl, preset.Image);
    }

    /// <summary>
    ///     Works out an address for an approved member upload.
    /// </summary>
    /// <param name="design">The member design.</param>
    /// <returns>The address, or null while the upload is waiting or missing.</returns>
    public async Task<string?> GetMemberUploadUrlAsync(CardDesign design)
    {
        if (design.UploadId is not { } id || design.ReviewStatus != (short)CardDesignReview.Approved)
            return null;

        await using var db = await dbFactory.CreateConnectionAsync();
        var url = await db.AchievementIconUploads.Where(x => x.GuildId == design.GuildId && x.Id == id)
            .Select(x => x.PublicUrl)
            .FirstOrDefaultAsync();
        return url ?? AchievementService.IconUploadPath(design.GuildId, id);
    }

    /// <summary>
    ///     Counts the uploads in the member pool.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The count.</returns>
    public Task<int> CountMemberUploadsAsync(ulong guildId)
    {
        return services.GetRequiredService<AchievementIconService>()
            .CountAsync(guildId, AchievementIconService.MemberCardKind);
    }

    #endregion

    #region Moderation

    /// <summary>
    ///     Approves a member design's waiting upload.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <returns>The outcome.</returns>
    public async Task<MemberModerationResult> ApproveMemberAsync(ulong guildId, int id)
    {
        var design = await GetMemberDesignByIdAsync(guildId, id);
        if (design is null)
            return new MemberModerationResult(MemberModerationOutcome.NotFound);
        if (design.ReviewStatus != (short)CardDesignReview.Pending || design.UploadId is null)
            return new MemberModerationResult(MemberModerationOutcome.NothingPending);

        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            await db.CardDesigns.Where(x => x.GuildId == guildId && x.Id == id)
                .Set(x => x.ReviewStatus, (short)CardDesignReview.Approved)
                .Set(x => x.ReviewNote, (string?)null)
                .UpdateAsync();
        }

        memberCache.TryRemove(guildId, out _);
        await services.GetRequiredService<AchievementIconService>().PublishApprovedAsync(guildId, design.UploadId.Value);
        return new MemberModerationResult(MemberModerationOutcome.Done);
    }

    /// <summary>
    ///     Turns down a member design's waiting upload and deletes it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <param name="reason">Why, or null.</param>
    /// <returns>The outcome.</returns>
    public async Task<MemberModerationResult> RejectMemberAsync(ulong guildId, int id, string? reason)
    {
        var design = await GetMemberDesignByIdAsync(guildId, id);
        if (design is null)
            return new MemberModerationResult(MemberModerationOutcome.NotFound);
        if (design.ReviewStatus != (short)CardDesignReview.Pending || design.UploadId is not { } uploadId)
            return new MemberModerationResult(MemberModerationOutcome.NothingPending);

        var note = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 300)];
        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            await db.CardDesigns.Where(x => x.GuildId == guildId && x.Id == id)
                .Set(x => x.UploadId, (int?)null)
                .Set(x => x.ReviewStatus, (short)CardDesignReview.Rejected)
                .Set(x => x.ReviewNote, note)
                .Set(x => x.DateUpdated, DateTime.UtcNow)
                .UpdateAsync();
        }

        memberCache.TryRemove(guildId, out _);
        await services.GetRequiredService<AchievementIconService>().DeleteAsync(guildId, uploadId);

        var strings = services.GetRequiredService<GeneratedBotStrings>();
        var text = strings.MemberDesignRejectedDm(guildId, GuildName(guildId), TargetName(strings, guildId, design));
        return new MemberModerationResult(MemberModerationOutcome.Done,
            await NotifyAsync(design.OwnerUserId!.Value, WithReason(strings, guildId, text, note)));
    }

    /// <summary>
    ///     Removes a member's design. The member can make a new one if they still qualify.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The design ID.</param>
    /// <param name="reason">Why, or null.</param>
    /// <returns>The outcome.</returns>
    public async Task<MemberModerationResult> RemoveMemberAsync(ulong guildId, int id, string? reason)
    {
        var design = await GetMemberDesignByIdAsync(guildId, id);
        if (design is null)
            return new MemberModerationResult(MemberModerationOutcome.NotFound);

        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            await db.CardDesigns.Where(x => x.GuildId == guildId && x.Id == id).DeleteAsync();
        }

        memberCache.TryRemove(guildId, out _);
        if (design.UploadId is { } uploadId)
            await services.GetRequiredService<AchievementIconService>().DeleteAsync(guildId, uploadId);

        var note = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 300)];
        var strings = services.GetRequiredService<GeneratedBotStrings>();
        var text = strings.MemberDesignRemovedDm(guildId, GuildName(guildId), TargetName(strings, guildId, design));
        return new MemberModerationResult(MemberModerationOutcome.Done,
            await NotifyAsync(design.OwnerUserId!.Value, WithReason(strings, guildId, text, note)));
    }

    /// <summary>
    ///     Clears the upload from member designs that pointed at one that was deleted.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="uploadId">The deleted upload.</param>
    private async Task DropMemberUploadAsync(ulong guildId, int uploadId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var changed = await db.CardDesigns.Where(x => x.GuildId == guildId && x.UploadId == uploadId)
            .Set(x => x.UploadId, (int?)null)
            .Set(x => x.ReviewStatus, (short)CardDesignReview.Approved)
            .UpdateAsync();
        if (changed > 0)
            memberCache.TryRemove(guildId, out _);
    }

    private string GuildName(ulong guildId)
    {
        return client.GetGuild(guildId)?.Name ?? guildId.ToString();
    }

    private static string TargetName(GeneratedBotStrings strings, ulong guildId, CardDesign design)
    {
        return design.Target == (short)CardDesignTarget.Rank
            ? strings.DesignTargetRank(guildId)
            : strings.DesignTargetAchievement(guildId);
    }

    private static string WithReason(GeneratedBotStrings strings, ulong guildId, string text, string? note)
    {
        return note is null ? text : $"{text}\n{strings.MemberDesignReason(guildId, note)}";
    }

    private async Task<bool> NotifyAsync(ulong userId, string text)
    {
        try
        {
            var user = (IUser?)client.GetUser(userId) ?? await client.Rest.GetUserAsync(userId);
            if (user is null)
                return false;

            await user.SendMessageAsync(text);
            return true;
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.CannotSendMessageToUser)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not tell {UserId} about their card design", userId);
            return false;
        }
    }

    #endregion
}
