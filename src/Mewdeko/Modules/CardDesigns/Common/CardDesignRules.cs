using System.Text.Json;
using DataModel;
using Mewdeko.Common.Cards;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Achievements.Common;

namespace Mewdeko.Modules.CardDesigns.Common;

/// <summary>
///     Where a rank card's background can come from.
/// </summary>
public enum RankSource
{
    /// <summary>
    ///     The design the member equipped.
    /// </summary>
    Equipped,

    /// <summary>
    ///     The member's own background.
    /// </summary>
    Personal,

    /// <summary>
    ///     The community default design.
    /// </summary>
    CommunityDefault,

    /// <summary>
    ///     The community's server wide background.
    /// </summary>
    Server,

    /// <summary>
    ///     The built in background.
    /// </summary>
    BuiltIn
}

/// <summary>
///     Whether a member may make their own design.
/// </summary>
public enum MemberEligibility
{
    /// <summary>
    ///     They may.
    /// </summary>
    Eligible,

    /// <summary>
    ///     The community doesn't let members make designs.
    /// </summary>
    Disabled,

    /// <summary>
    ///     They need one of the chosen roles.
    /// </summary>
    NeedsRole,

    /// <summary>
    ///     They need a higher level.
    /// </summary>
    NeedsLevel
}

/// <summary>
///     Pure rules for card designs: validation, who unlocks what, and which design a card is drawn with.
/// </summary>
public static class CardDesignRules
{
    /// <summary>
    ///     Longest design name.
    /// </summary>
    public const int MaxNameLength = 40;

    /// <summary>
    ///     Highest level a design can unlock at.
    /// </summary>
    public const int MaxUnlockLevel = 1000;

    /// <summary>
    ///     The order rank card backgrounds are tried in. The first source that has an image wins.
    /// </summary>
    public static readonly IReadOnlyList<RankSource> RankOrder =
    [
        RankSource.Equipped, RankSource.Personal, RankSource.CommunityDefault, RankSource.Server, RankSource.BuiltIn
    ];

    /// <summary>
    ///     Checks a design's name and unlock settings.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="availability">How members get it.</param>
    /// <param name="unlockType">What grants it, for unlock designs.</param>
    /// <param name="level">The unlock level.</param>
    /// <param name="achievementKey">The unlock achievement.</param>
    /// <param name="roleId">The unlock role.</param>
    /// <returns>What is wrong, or null.</returns>
    public static string? Validate(string? name, CardDesignAvailability availability, CardDesignUnlockType? unlockType,
        int? level, string? achievementKey, ulong? roleId)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return "Name is required.";
        if (trimmed.Length > MaxNameLength)
            return $"Name can't be longer than {MaxNameLength} characters.";
        if (!Enum.IsDefined(availability))
            return "Availability must be 0, 1 or 2.";
        if (availability != CardDesignAvailability.Unlock)
            return null;

        return unlockType switch
        {
            CardDesignUnlockType.Level when level is null or < 1 or > MaxUnlockLevel =>
                $"Unlock level must be between 1 and {MaxUnlockLevel}.",
            CardDesignUnlockType.Achievement when string.IsNullOrWhiteSpace(achievementKey) =>
                "Pick the achievement that unlocks it.",
            CardDesignUnlockType.Role when roleId is null or 0 => "Pick the role that unlocks it.",
            CardDesignUnlockType.Level or CardDesignUnlockType.Achievement or CardDesignUnlockType.Role => null,
            _ => "Unlock type must be 0, 1 or 2."
        };
    }

    /// <summary>
    ///     Clears the unlock fields a design doesn't use.
    /// </summary>
    /// <param name="design">The design, changed in place.</param>
    public static void ClearUnusedUnlock(CardDesign design)
    {
        if (design.Availability != (short)CardDesignAvailability.Unlock)
        {
            design.UnlockType = null;
            design.UnlockLevel = null;
            design.UnlockAchievementKey = null;
            design.UnlockRoleId = null;
            return;
        }

        if (design.UnlockType != (short)CardDesignUnlockType.Level)
            design.UnlockLevel = null;
        if (design.UnlockType != (short)CardDesignUnlockType.Achievement)
            design.UnlockAchievementKey = null;
        if (design.UnlockType != (short)CardDesignUnlockType.Role)
            design.UnlockRoleId = null;
    }

    /// <summary>
    ///     Whether a member's standing unlocks a design.
    /// </summary>
    /// <param name="design">The design.</param>
    /// <param name="level">The member's level, when known.</param>
    /// <param name="achievements">The achievement keys the member holds, when known.</param>
    /// <param name="roles">The role IDs the member holds, when known.</param>
    /// <returns>True when the design is an unlock design and the member meets its condition.</returns>
    public static bool Unlocks(CardDesign design, int? level, IReadOnlyCollection<string>? achievements,
        IReadOnlyCollection<ulong>? roles)
    {
        if (design.Availability != (short)CardDesignAvailability.Unlock)
            return false;

        return (CardDesignUnlockType?)design.UnlockType switch
        {
            CardDesignUnlockType.Level => level is { } current && design.UnlockLevel is { } needed && current >= needed,
            CardDesignUnlockType.Achievement => design.UnlockAchievementKey is { } key &&
                                                achievements?.Contains(key, StringComparer.OrdinalIgnoreCase) == true,
            CardDesignUnlockType.Role => design.UnlockRoleId is { } role && roles?.Contains(role) == true,
            _ => false
        };
    }

    /// <summary>
    ///     Whether a member can equip a design.
    /// </summary>
    /// <param name="design">The design.</param>
    /// <param name="owns">Whether the member owns it.</param>
    /// <returns>True for free designs and owned ones.</returns>
    public static bool CanEquip(CardDesign design, bool owns)
    {
        return design.OwnerUserId is not null
            ? owns
            : design.Availability == (short)CardDesignAvailability.Free || owns;
    }

    /// <summary>
    ///     Most a member design can cost to make or update.
    /// </summary>
    public const long MaxMemberCost = 100_000_000;

    /// <summary>
    ///     Highest level a community can ask of members who make designs.
    /// </summary>
    public const int MaxMemberLevel = 1000;

    /// <summary>
    ///     Most roles a community can pick as eligible.
    /// </summary>
    public const int MaxMemberRoles = 25;

    /// <summary>
    ///     The name a member design is stored under. Members never see or type it.
    /// </summary>
    /// <param name="userId">The member.</param>
    /// <param name="target">The card.</param>
    /// <returns>The name.</returns>
    public static string MemberName(ulong userId, CardDesignTarget target)
    {
        return $"member-{userId}-{(int)target}";
    }

    /// <summary>
    ///     Whether a member standing lets them make a design.
    /// </summary>
    /// <param name="settings">The community's rules.</param>
    /// <param name="level">The member's level.</param>
    /// <param name="roles">The roles the member holds.</param>
    /// <returns>Whether they can, or what they are missing.</returns>
    public static MemberEligibility CheckMember(CardDesignSetting settings, int level, IReadOnlyCollection<ulong> roles)
    {
        if (!settings.MemberEnabled)
            return MemberEligibility.Disabled;

        return (MemberDesignWho)settings.MemberWho switch
        {
            MemberDesignWho.Roles => ParseRoleIds(settings.MemberRoleIds).Any(roles.Contains)
                ? MemberEligibility.Eligible
                : MemberEligibility.NeedsRole,
            MemberDesignWho.Level => level >= settings.MemberMinLevel
                ? MemberEligibility.Eligible
                : MemberEligibility.NeedsLevel,
            _ => MemberEligibility.Eligible
        };
    }

    /// <summary>
    ///     Reads the stored comma separated role IDs.
    /// </summary>
    /// <param name="stored">The stored text.</param>
    /// <returns>The role IDs.</returns>
    public static IReadOnlyList<ulong> ParseRoleIds(string? stored)
    {
        return (stored ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => ulong.TryParse(s, out var id) ? id : 0)
            .Where(id => id != 0)
            .Distinct()
            .ToList();
    }

    /// <summary>
    ///     Writes role IDs for storage.
    /// </summary>
    /// <param name="ids">The role IDs.</param>
    /// <returns>The stored text.</returns>
    public static string FormatRoleIds(IEnumerable<ulong> ids)
    {
        return string.Join(',', ids.Where(id => id != 0).Distinct().Take(MaxMemberRoles));
    }

    /// <summary>
    ///     Checks a community's member design rules.
    /// </summary>
    /// <param name="who">Who may make designs.</param>
    /// <param name="roleCount">How many roles are picked.</param>
    /// <param name="level">The level asked for.</param>
    /// <param name="cost">What a save costs.</param>
    /// <returns>What is wrong, or null.</returns>
    public static string? ValidateMemberSettings(int who, int roleCount, int level, long cost)
    {
        if (!Enum.IsDefined((MemberDesignWho)who))
            return "Who must be 0, 1 or 2.";
        if (roleCount > MaxMemberRoles)
            return $"Pick at most {MaxMemberRoles} roles.";
        if (level is < 1 or > MaxMemberLevel)
            return $"Level must be between 1 and {MaxMemberLevel}.";
        if (cost is < 0 or > MaxMemberCost)
            return $"Cost must be between 0 and {MaxMemberCost}.";

        return null;
    }

    /// <summary>
    ///     Checks that a member design only uses preset images. Members never link their own images, community
    ///     uploads or web addresses.
    /// </summary>
    /// <param name="template">The achievement template.</param>
    /// <returns>What is wrong, or null.</returns>
    public static string? MemberTemplateProblem(CardTemplate template)
    {
        if (!IsPresetOrEmpty(template.Background.Url))
            return "Members can only use preset backgrounds.";
        if (template.Elements.Any(e => !IsPresetOrEmpty(e.Url)))
            return "Members can only use preset images.";

        return null;
    }

    /// <summary>
    ///     Works out the review status after a member saves.
    /// </summary>
    /// <param name="hasUpload">Whether the design holds an uploaded background after the save.</param>
    /// <param name="uploadedNow">Whether this save added a new upload.</param>
    /// <param name="review">Whether the community reviews uploads.</param>
    /// <param name="current">The status before the save.</param>
    /// <returns>The new status.</returns>
    public static CardDesignReview NextReview(bool hasUpload, bool uploadedNow, bool review, CardDesignReview current)
    {
        if (!hasUpload)
            return CardDesignReview.Approved;
        if (uploadedNow)
            return review ? CardDesignReview.Pending : CardDesignReview.Approved;

        return current;
    }

    /// <summary>
    ///     The data a card is drawn from, with an approved uploaded background laid over the design's own data.
    /// </summary>
    /// <param name="design">The design.</param>
    /// <returns>The data to draw.</returns>
    public static string EffectiveData(CardDesign design)
    {
        return design is { OwnerUserId: not null, UploadId: { } id } &&
               design.ReviewStatus == (short)CardDesignReview.Approved
            ? WithBackground((CardDesignTarget)design.Target, design.Data, "upload:" + id)
            : design.Data;
    }

    private static bool IsPresetOrEmpty(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) || CardPresets.TryParse(text, out _);
    }

    /// <summary>
    ///     Picks the design a card is drawn with: the member's equipped design, else the community default, else none.
    /// </summary>
    /// <param name="equipped">The equipped design, if any.</param>
    /// <param name="ownsEquipped">Whether the member owns it.</param>
    /// <param name="communityDefault">The community default for the target, if any.</param>
    /// <returns>The design, or null for the built in one.</returns>
    public static CardDesign? Pick(CardDesign? equipped, bool ownsEquipped, CardDesign? communityDefault)
    {
        return equipped is not null && CanEquip(equipped, ownsEquipped) ? equipped : communityDefault;
    }

    /// <summary>
    ///     Finds a design by name, case-insensitively.
    /// </summary>
    /// <param name="designs">The designs to search.</param>
    /// <param name="name">The name as typed.</param>
    /// <returns>The design, or null.</returns>
    public static CardDesign? FindByName(IEnumerable<CardDesign> designs, string? name)
    {
        var wanted = name?.Trim();
        return string.IsNullOrEmpty(wanted)
            ? null
            : designs.FirstOrDefault(d => d.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Checks that a background reference is empty, a preset, an upload, or a web address.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>True when its shape is acceptable.</returns>
    public static bool IsValidBackgroundRef(string? reference)
    {
        var value = reference?.Trim();
        if (string.IsNullOrEmpty(value))
            return true;
        if (CardPresets.TryParse(value, out _))
            return true;
        if (value.StartsWith("upload:", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value["upload:".Length..], out var id) && id > 0;

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }

    /// <summary>
    ///     Builds the stored data of a rank design.
    /// </summary>
    /// <param name="background">The background reference, or null for none.</param>
    /// <returns>The JSON.</returns>
    public static string RankData(string? background)
    {
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["background"] = background?.Trim() ?? ""
        });
    }

    /// <summary>
    ///     Reads the background reference out of a design's data.
    /// </summary>
    /// <param name="target">The design's target.</param>
    /// <param name="data">The stored data.</param>
    /// <returns>The reference, or null when the design has no image background.</returns>
    public static string? BackgroundRef(CardDesignTarget target, string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return null;

        try
        {
            if (target == CardDesignTarget.Rank)
            {
                using var document = JsonDocument.Parse(data);
                var value = document.RootElement.ValueKind == JsonValueKind.Object &&
                            document.RootElement.TryGetProperty("background", out var property) &&
                            property.ValueKind == JsonValueKind.String
                    ? property.GetString()?.Trim()
                    : null;
                return string.IsNullOrEmpty(value) ? null : value;
            }

            var template = JsonSerializer.Deserialize<CardTemplate>(data, CardTemplate.Json);
            return template is { Background: { Kind: "image" } background } && !string.IsNullOrWhiteSpace(background.Url)
                ? background.Url.Trim()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Sets or clears the background of a design's data, keeping everything else.
    /// </summary>
    /// <param name="target">The design's target.</param>
    /// <param name="data">The stored data.</param>
    /// <param name="reference">The new background reference, or null to clear it.</param>
    /// <returns>The new data.</returns>
    public static string WithBackground(CardDesignTarget target, string? data, string? reference)
    {
        if (target == CardDesignTarget.Rank)
            return RankData(reference);

        var template = (string.IsNullOrWhiteSpace(data)
                ? null
                : TryReadTemplate(data)) ?? AchievementCards.Default();
        if (string.IsNullOrWhiteSpace(reference))
        {
            template.Background.Kind = "palette";
            template.Background.Url = "";
        }
        else
        {
            template.Background.Kind = "image";
            template.Background.Url = reference.Trim();
        }

        return template.Serialize();
    }

    private static CardTemplate? TryReadTemplate(string data)
    {
        try
        {
            return JsonSerializer.Deserialize<CardTemplate>(data, CardTemplate.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
