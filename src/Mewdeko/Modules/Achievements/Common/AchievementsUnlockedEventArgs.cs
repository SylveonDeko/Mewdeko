namespace Mewdeko.Modules.Achievements.Common;

/// <summary>
///     Published when a member unlocks achievements in a guild.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="UserId">The member.</param>
/// <param name="Keys">The keys of the achievements newly unlocked.</param>
public record AchievementsUnlockedEventArgs(ulong GuildId, ulong UserId, IReadOnlyList<string> Keys);
