using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Minecraft.Common;

/// <summary>
///     Stores customizable message templates for all Minecraft bridge events.
///     Discord templates use the SmartEmbed JSON format with MC placeholders.
///     In-game templates use Minecraft formatting codes.
/// </summary>
public class McEventTemplates
{
    /// <summary>
    ///     Gets or sets the Discord embed template for player join events.
    /// </summary>
    [JsonPropertyName("joinDiscord")]
    public string? JoinDiscord { get; set; }

    /// <summary>
    ///     Gets or sets the in-game message format for Discord chat relayed to MC.
    ///     Placeholders: %user%, %message%, %channel%
    /// </summary>
    [JsonPropertyName("joinIngame")]
    public string? JoinIngame { get; set; }

    /// <summary>
    ///     Gets or sets the Discord embed template for player leave events.
    /// </summary>
    [JsonPropertyName("leaveDiscord")]
    public string? LeaveDiscord { get; set; }

    /// <summary>
    ///     Gets or sets the in-game message format for player leave.
    /// </summary>
    [JsonPropertyName("leaveIngame")]
    public string? LeaveIngame { get; set; }

    /// <summary>
    ///     Gets or sets the Discord message template for chat relay.
    ///     Placeholders: %mc.player%, %mc.message%
    /// </summary>
    [JsonPropertyName("chatDiscord")]
    public string? ChatDiscord { get; set; }

    /// <summary>
    ///     Gets or sets the in-game message format for Discord chat relayed to MC.
    ///     Placeholders: %user%, %message%, %channel%
    /// </summary>
    [JsonPropertyName("chatIngame")]
    public string? ChatIngame { get; set; }

    /// <summary>
    ///     Gets or sets the Discord embed template for death events.
    ///     Placeholders: %mc.player%, %mc.death.message%
    /// </summary>
    [JsonPropertyName("deathDiscord")]
    public string? DeathDiscord { get; set; }

    /// <summary>
    ///     Gets or sets the Discord embed template for advancement events.
    ///     Placeholders: %mc.player%, %mc.advancement%
    /// </summary>
    [JsonPropertyName("advancementDiscord")]
    public string? AdvancementDiscord { get; set; }
}