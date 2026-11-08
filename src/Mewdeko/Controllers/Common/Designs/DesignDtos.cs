using System.Text.Json;

namespace Mewdeko.Controllers.Common.Designs;

/// <summary>
///     Request to create or change a card design.
/// </summary>
public class DesignRequest
{
    /// <summary>The design's name, unique per community. Up to 40 characters.</summary>
    public string? Name { get; set; }

    /// <summary>The card: 0 for rank, 1 for achievement. Fixed once the design exists.</summary>
    public int Target { get; set; }

    /// <summary>
    ///     The design. Rank designs take <c>{ "background": "preset:id" | "upload:id" | "https://..." }</c>, achievement
    ///     designs take a card template. Omitted on an update keeps the current design.
    /// </summary>
    public JsonElement? Data { get; set; }

    /// <summary>How members get it: 0 free, 1 shop, 2 unlock.</summary>
    public int Availability { get; set; }

    /// <summary>For unlock designs: 0 level, 1 achievement, 2 role.</summary>
    public int? UnlockType { get; set; }

    /// <summary>The level that unlocks it.</summary>
    public int? UnlockLevel { get; set; }

    /// <summary>The achievement key that unlocks it.</summary>
    public string? UnlockAchievementKey { get; set; }

    /// <summary>The role that unlocks it.</summary>
    public ulong? UnlockRoleId { get; set; }
}

/// <summary>
///     Request to set or clear a design's background image.
/// </summary>
public class DesignBackgroundRequest
{
    /// <summary>The image as raw base64 or a <c>data:image/...;base64,</c> URI. Stored as one of the community's card images.</summary>
    public string? Data { get; set; }

    /// <summary>A <c>preset:id</c> reference, or an image address the bot downloads and stores as a card image.</summary>
    public string? Url { get; set; }
}

/// <summary>
///     Request to equip a design.
/// </summary>
public class EquipDesignRequest
{
    /// <summary>The design to equip.</summary>
    public int DesignId { get; set; }
}

/// <summary>
///     Request to unequip the design of one card.
/// </summary>
public class UnequipDesignRequest
{
    /// <summary>The card: 0 for rank, 1 for achievement.</summary>
    public int Target { get; set; }
}

/// <summary>
///     A design as the dashboard's admin pages see it.
/// </summary>
public class DesignResponse
{
    /// <summary>The design's ID.</summary>
    public int Id { get; set; }

    /// <summary>The design's name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>The card: 0 for rank, 1 for achievement.</summary>
    public int Target { get; set; }

    /// <summary>How members get it: 0 free, 1 shop, 2 unlock.</summary>
    public int Availability { get; set; }

    /// <summary>For unlock designs: 0 level, 1 achievement, 2 role.</summary>
    public int? UnlockType { get; set; }

    /// <summary>The level that unlocks it.</summary>
    public int? UnlockLevel { get; set; }

    /// <summary>The achievement key that unlocks it.</summary>
    public string? UnlockAchievementKey { get; set; }

    /// <summary>The name of that achievement, if it still exists.</summary>
    public string? UnlockAchievementName { get; set; }

    /// <summary>The role that unlocks it.</summary>
    public ulong? UnlockRoleId { get; set; }

    /// <summary>The name of that role, if it still exists.</summary>
    public string? UnlockRoleName { get; set; }

    /// <summary>Whether it is the community default for its card.</summary>
    public bool IsDefault { get; set; }

    /// <summary>The design: a background reference for rank designs, a card template for achievement designs.</summary>
    public JsonElement Data { get; set; }

    /// <summary>The background reference, or null when the design has no image background.</summary>
    public string? BackgroundRef { get; set; }

    /// <summary>An address a browser can load the background from, for previews.</summary>
    public string? BackgroundUrl { get; set; }

    /// <summary>How many members own it.</summary>
    public int OwnedCount { get; set; }

    /// <summary>The shop item selling it, if any.</summary>
    public int? ShopItemId { get; set; }

    /// <summary>The name of that shop item.</summary>
    public string? ShopItemName { get; set; }

    /// <summary>When it was created.</summary>
    public DateTime DateAdded { get; set; }

    /// <summary>When it last changed.</summary>
    public DateTime DateUpdated { get; set; }
}

/// <summary>
///     A design a member can use.
/// </summary>
public class MemberDesignResponse
{
    /// <summary>The design's ID.</summary>
    public int Id { get; set; }

    /// <summary>The design's name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>The card: 0 for rank, 1 for achievement.</summary>
    public int Target { get; set; }

    /// <summary>How members get it: 0 free, 1 shop, 2 unlock.</summary>
    public int Availability { get; set; }

    /// <summary>How the member got it: free, purchase, level, achievement or role.</summary>
    public string Source { get; set; } = "free";

    /// <summary>Whether the member has it equipped.</summary>
    public bool Equipped { get; set; }

    /// <summary>Whether it is the community default.</summary>
    public bool IsDefault { get; set; }

    /// <summary>The design, for previews.</summary>
    public JsonElement Data { get; set; }

    /// <summary>An address a browser can load the background from, for previews.</summary>
    public string? BackgroundUrl { get; set; }
}

/// <summary>
///     The designs a member can use for one card.
/// </summary>
public class MemberDesignSectionResponse
{
    /// <summary>The ID of the design the member has equipped, if any.</summary>
    public int? EquippedId { get; set; }

    /// <summary>The ID of the community default design, if any.</summary>
    public int? DefaultId { get; set; }

    /// <summary>The designs the member owns or that are free.</summary>
    public List<MemberDesignResponse> Designs { get; set; } = [];
}

/// <summary>
///     Everything a member can use in one community.
/// </summary>
public class MemberDesignsResponse
{
    /// <summary>The community.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The member.</summary>
    public ulong UserId { get; set; }

    /// <summary>Designs for the rank card.</summary>
    public MemberDesignSectionResponse Rank { get; set; } = new();

    /// <summary>Designs for the achievement card.</summary>
    public MemberDesignSectionResponse Achievement { get; set; } = new();
}

/// <summary>
///     A community's rules for designs its members make.
/// </summary>
public class MemberDesignSettingsResponse
{
    /// <summary>Whether members can make designs.</summary>
    public bool Enabled { get; set; }

    /// <summary>Who may: 0 everyone, 1 members with a chosen role, 2 members at or above a level.</summary>
    public int Who { get; set; }

    /// <summary>The roles that may, for the roles option.</summary>
    public List<ulong> RoleIds { get; set; } = [];

    /// <summary>The level members need, for the level option.</summary>
    public int MinLevel { get; set; } = 1;

    /// <summary>Whether members may upload a background as well as pick a preset.</summary>
    public bool Uploads { get; set; }

    /// <summary>Whether an uploaded background waits for a moderator.</summary>
    public bool Review { get; set; } = true;

    /// <summary>What making or updating a design costs, or 0 for free.</summary>
    public long Cost { get; set; }

    /// <summary>How many member uploads the community holds.</summary>
    public int UploadsUsed { get; set; }

    /// <summary>The size of the member upload pool.</summary>
    public int UploadsMax { get; set; }

    /// <summary>How many designs wait for review.</summary>
    public int Pending { get; set; }
}

/// <summary>
///     Request to change a community's member design rules. Fields left out keep their value.
/// </summary>
public class MemberDesignSettingsRequest
{
    /// <summary>Whether members can make designs.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Who may: 0 everyone, 1 members with a chosen role, 2 members at or above a level.</summary>
    public int? Who { get; set; }

    /// <summary>The roles that may.</summary>
    public List<ulong>? RoleIds { get; set; }

    /// <summary>The level members need.</summary>
    public int? MinLevel { get; set; }

    /// <summary>Whether members may upload a background.</summary>
    public bool? Uploads { get; set; }

    /// <summary>Whether an uploaded background waits for a moderator.</summary>
    public bool? Review { get; set; }

    /// <summary>What making or updating a design costs.</summary>
    public long? Cost { get; set; }
}

/// <summary>
///     A member's design as the moderation list shows it.
/// </summary>
public class MemberDesignAdminResponse
{
    /// <summary>The design's ID.</summary>
    public int Id { get; set; }

    /// <summary>The creator.</summary>
    public ulong UserId { get; set; }

    /// <summary>The creator's name, when they are still in the community.</summary>
    public string? UserName { get; set; }

    /// <summary>The creator's avatar address.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>The card: 0 for rank, 1 for achievement.</summary>
    public int Target { get; set; }

    /// <summary>approved, pending or rejected.</summary>
    public string Status { get; set; } = "approved";

    /// <summary>The reason given when an upload was turned down.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>Whether the design holds an uploaded background.</summary>
    public bool HasUpload { get; set; }

    /// <summary>Whether the creator still qualifies.</summary>
    public bool Active { get; set; }

    /// <summary>The design, with its preset choice as the fallback background.</summary>
    public JsonElement Data { get; set; }

    /// <summary>An address for the preset background, if any.</summary>
    public string? BackgroundUrl { get; set; }

    /// <summary>When it was created.</summary>
    public DateTime DateAdded { get; set; }

    /// <summary>When it last changed.</summary>
    public DateTime DateUpdated { get; set; }
}

/// <summary>
///     The member designs of a community with the pool figures.
/// </summary>
public class MemberDesignListResponse
{
    /// <summary>The designs.</summary>
    public List<MemberDesignAdminResponse> Items { get; set; } = [];

    /// <summary>How many member uploads the community holds.</summary>
    public int UploadsUsed { get; set; }

    /// <summary>The size of the member upload pool.</summary>
    public int UploadsMax { get; set; }

    /// <summary>How many designs wait for review.</summary>
    public int Pending { get; set; }
}

/// <summary>
///     Request to turn down or remove a member design.
/// </summary>
public class MemberDesignModerationRequest
{
    /// <summary>Why, shown to the member. Up to 300 characters.</summary>
    public string? Reason { get; set; }
}

/// <summary>
///     The result of turning down or removing a member design.
/// </summary>
public class MemberDesignModerationResponse
{
    /// <summary>Whether the member was told. False when their DMs are closed.</summary>
    public bool Notified { get; set; }
}

/// <summary>
///     A member's own design for one card.
/// </summary>
public class OwnDesignResponse
{
    /// <summary>The design's ID.</summary>
    public int Id { get; set; }

    /// <summary>The card: 0 for rank, 1 for achievement.</summary>
    public int Target { get; set; }

    /// <summary>approved, pending or rejected.</summary>
    public string Status { get; set; } = "approved";

    /// <summary>The reason given when an upload was turned down.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>Whether the member has it equipped.</summary>
    public bool Equipped { get; set; }

    /// <summary>Whether it renders right now: the community allows member designs and the member qualifies.</summary>
    public bool Active { get; set; }

    /// <summary>The design with the member's preset choice as the fallback background.</summary>
    public JsonElement Data { get; set; }

    /// <summary>An address for the preset background, if any.</summary>
    public string? BackgroundUrl { get; set; }

    /// <summary>Whether the design holds an uploaded background.</summary>
    public bool HasUpload { get; set; }

    /// <summary>An address for the uploaded background once approved. Pending uploads load from the image route.</summary>
    public string? UploadUrl { get; set; }

    /// <summary>When it last changed.</summary>
    public DateTime DateUpdated { get; set; }
}

/// <summary>
///     What a member can do about making their own designs in one community.
/// </summary>
public class OwnDesignsResponse
{
    /// <summary>Whether the community lets members make designs.</summary>
    public bool Enabled { get; set; }

    /// <summary>Whether this member may make one now.</summary>
    public bool Eligible { get; set; }

    /// <summary>Why not: disabled, role or level.</summary>
    public string? Reason { get; set; }

    /// <summary>Who may: 0 everyone, 1 role, 2 level.</summary>
    public int Who { get; set; }

    /// <summary>The names of the roles that qualify, for the roles option.</summary>
    public List<string> RoleNames { get; set; } = [];

    /// <summary>The level needed, for the level option.</summary>
    public int MinLevel { get; set; }

    /// <summary>Whether uploads are allowed.</summary>
    public bool Uploads { get; set; }

    /// <summary>Whether an uploaded background waits for a moderator.</summary>
    public bool Review { get; set; }

    /// <summary>What a save costs, or 0 for free.</summary>
    public long Cost { get; set; }

    /// <summary>The community's currency emote.</summary>
    public string? CurrencyEmote { get; set; }

    /// <summary>The member's rank design, if they made one.</summary>
    public OwnDesignResponse? Rank { get; set; }

    /// <summary>The member's achievement design, if they made one.</summary>
    public OwnDesignResponse? Achievement { get; set; }
}

/// <summary>
///     Request to create or replace the member's own design for one card.
/// </summary>
public class SaveOwnDesignRequest
{
    /// <summary>
    ///     The design: <c>{ "background": "preset:id" }</c> for rank, a card template with preset images for achievement.
    ///     Omitted keeps the current design.
    /// </summary>
    public JsonElement? Data { get; set; }

    /// <summary>A new background as raw base64 or a <c>data:image/...;base64,</c> URI. Needs uploads to be allowed.</summary>
    public string? Upload { get; set; }

    /// <summary>Drops the uploaded background.</summary>
    public bool RemoveUpload { get; set; }
}

/// <summary>
///     An uploaded background as a data address.
/// </summary>
public class DesignImageResponse
{
    /// <summary>The image as a <c>data:image/png;base64,</c> address, or null when there is none.</summary>
    public string? DataUrl { get; set; }
}

/// <summary>
///     A community the member shares with the bot, for the cross community designs page.
/// </summary>
public class DesignGuildResponse
{
    /// <summary>The community's ID.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The community's name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>The community's icon address.</summary>
    public string? IconUrl { get; set; }

    /// <summary>How many designs the member can use there.</summary>
    public int Available { get; set; }
}
