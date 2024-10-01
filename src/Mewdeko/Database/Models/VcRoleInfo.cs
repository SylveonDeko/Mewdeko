using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents information about voice channel roles in a guild.
/// </summary>
[Table("VcRoleInfo")]
public class VcRoleInfo : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild configuration ID.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    ///     Gets or sets the voice channel ID.
    /// </summary>
    public ulong VoiceChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the role ID.
    /// </summary>
    public ulong RoleId { get; set; }
}