using System.Text.Json;
using DataModel;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.CardDesigns.Common;
using Mewdeko.Modules.CardDesigns.Services;

namespace Mewdeko.Controllers.Common.Designs;

/// <summary>
///     Turns stored designs into the responses the dashboard reads.
/// </summary>
public static class DesignMapping
{
    /// <summary>
    ///     Reads stored design data as JSON, falling back to an empty object.
    /// </summary>
    /// <param name="data">The stored data.</param>
    /// <returns>The data as a JSON element.</returns>
    public static JsonElement ParseData(string? data)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(data) ? "{}" : data);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var document = JsonDocument.Parse("{}");
            return document.RootElement.Clone();
        }
    }

    /// <summary>
    ///     Decodes an uploaded image sent as raw base64 or a data address.
    /// </summary>
    /// <param name="data">The text as sent.</param>
    /// <returns>The bytes, or null when it isn't base64.</returns>
    public static byte[]? DecodeUpload(string data)
    {
        var payload = data.Trim();
        var comma = payload.IndexOf(',');
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            payload = payload[(comma + 1)..];
        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Copies a member design with the data a card is drawn from, so previews match the card.
    /// </summary>
    /// <param name="design">The member design.</param>
    /// <returns>The copy.</returns>
    public static CardDesign Effective(CardDesign design)
    {
        return new CardDesign
        {
            Id = design.Id,
            GuildId = design.GuildId,
            Name = design.Name,
            Target = design.Target,
            Data = CardDesignRules.EffectiveData(design),
            Availability = design.Availability,
            OwnerUserId = design.OwnerUserId
        };
    }

    /// <summary>
    ///     The review state as the dashboard reads it.
    /// </summary>
    /// <param name="design">The member design.</param>
    /// <returns>approved, pending or rejected.</returns>
    public static string StatusName(CardDesign design)
    {
        return (CardDesignReview)design.ReviewStatus switch
        {
            CardDesignReview.Pending => "pending",
            CardDesignReview.Rejected => "rejected",
            _ => "approved"
        };
    }

    /// <summary>
    ///     Builds the response for a member's own design.
    /// </summary>
    /// <param name="designs">The design library.</param>
    /// <param name="design">The member design.</param>
    /// <param name="equippedId">The ID the member has equipped for that card, if any.</param>
    /// <returns>The response.</returns>
    public static async Task<OwnDesignResponse> MapOwnAsync(CardDesignService designs, CardDesign design,
        int? equippedId)
    {
        return new OwnDesignResponse
        {
            Id = design.Id,
            Target = design.Target,
            Status = StatusName(design),
            ReviewNote = design.ReviewNote,
            Equipped = equippedId == design.Id,
            Active = await designs.IsMemberUsableAsync(design),
            Data = ParseData(design.Data),
            BackgroundUrl = await designs.GetMemberPresetUrlAsync(design),
            HasUpload = design.UploadId is not null,
            UploadUrl = await designs.GetMemberUploadUrlAsync(design),
            DateUpdated = design.DateUpdated
        };
    }

    /// <summary>
    ///     Builds the admin response for a design.
    /// </summary>
    /// <param name="info">The design with its figures.</param>
    /// <param name="backgroundUrl">The address of its background, if any.</param>
    /// <param name="achievementName">The name of the unlock achievement, if any.</param>
    /// <param name="roleName">The name of the unlock role, if any.</param>
    /// <returns>The response.</returns>
    public static DesignResponse Map(CardDesignInfo info, string? backgroundUrl, string? achievementName,
        string? roleName)
    {
        var design = info.Design;
        return new DesignResponse
        {
            Id = design.Id,
            Name = design.Name,
            Target = design.Target,
            Availability = design.Availability,
            UnlockType = design.UnlockType,
            UnlockLevel = design.UnlockLevel,
            UnlockAchievementKey = design.UnlockAchievementKey,
            UnlockAchievementName = achievementName,
            UnlockRoleId = design.UnlockRoleId,
            UnlockRoleName = roleName,
            IsDefault = design.IsDefault,
            Data = ParseData(design.Data),
            BackgroundRef = CardDesignRules.BackgroundRef((CardDesignTarget)design.Target, design.Data),
            BackgroundUrl = backgroundUrl,
            OwnedCount = info.OwnedCount,
            ShopItemId = info.ShopItemId,
            ShopItemName = info.ShopItemName,
            DateAdded = design.DateAdded,
            DateUpdated = design.DateUpdated
        };
    }

    /// <summary>
    ///     Builds what a member can use in one community.
    /// </summary>
    /// <param name="designs">The design library.</param>
    /// <param name="guildId">The community.</param>
    /// <param name="userId">The member.</param>
    /// <returns>The member's designs per card.</returns>
    public static async Task<MemberDesignsResponse> BuildMemberAsync(CardDesignService designs, ulong guildId,
        ulong userId)
    {
        var available = await designs.GetAvailableAsync(guildId, userId);
        var equipped = await designs.GetEquippedIdsAsync(guildId, userId);
        var urls = await designs.ResolveBackgroundUrlsAsync(guildId, available.Select(a => a.Design));

        var response = new MemberDesignsResponse
        {
            GuildId = guildId, UserId = userId
        };

        foreach (var (design, source) in available)
        {
            var target = (CardDesignTarget)design.Target;
            var section = target == CardDesignTarget.Rank ? response.Rank : response.Achievement;
            var isEquipped = equipped.TryGetValue(target, out var equippedId) && equippedId == design.Id;
            if (isEquipped)
                section.EquippedId = design.Id;
            if (design.IsDefault)
                section.DefaultId = design.Id;

            section.Designs.Add(new MemberDesignResponse
            {
                Id = design.Id,
                Name = design.Name,
                Target = design.Target,
                Availability = design.Availability,
                Source = source?.ToString().ToLowerInvariant() ?? "free",
                Equipped = isEquipped,
                IsDefault = design.IsDefault,
                Data = ParseData(design.Data),
                BackgroundUrl = urls.GetValueOrDefault(design.Id)
            });
        }

        foreach (var target in new[] { CardDesignTarget.Rank, CardDesignTarget.Achievement })
        {
            var own = await designs.GetMemberDesignAsync(guildId, userId, target);
            if (own is null || !await designs.IsMemberUsableAsync(own))
                continue;

            var effective = Effective(own);
            var section = target == CardDesignTarget.Rank ? response.Rank : response.Achievement;
            var isEquipped = equipped.TryGetValue(target, out var equippedId) && equippedId == own.Id;
            if (isEquipped)
                section.EquippedId = own.Id;

            section.Designs.Add(new MemberDesignResponse
            {
                Id = own.Id,
                Name = "",
                Target = own.Target,
                Availability = own.Availability,
                Source = "own",
                Equipped = isEquipped,
                IsDefault = false,
                Data = ParseData(effective.Data),
                BackgroundUrl = (await designs.ResolveBackgroundUrlsAsync(guildId, [effective])).GetValueOrDefault(own.Id)
            });
        }

        foreach (var design in await designs.GetDesignsAsync(guildId))
        {
            if (!design.IsDefault || available.Any(a => a.Design.Id == design.Id))
                continue;

            if (design.Target == (short)CardDesignTarget.Rank)
                response.Rank.DefaultId = design.Id;
            else
                response.Achievement.DefaultId = design.Id;
        }

        return response;
    }
}
