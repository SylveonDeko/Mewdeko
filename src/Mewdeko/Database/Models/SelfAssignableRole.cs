namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a self-assigned role in a guild.
/// </summary>
public class SelfAssignedRole : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the role ID.
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Gets or sets the group number.
    /// </summary>
    public int Group { get; set; }

    /// <summary>
    ///     Gets or sets the level requirement for the role.
    /// </summary>
    public int LevelRequirement { get; set; }
}