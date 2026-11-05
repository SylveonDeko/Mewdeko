namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Event arguments for when an inviter's net invite count changes.
/// </summary>
/// <param name="GuildId">The guild.</param>
/// <param name="UserId">The inviter.</param>
/// <param name="Count">The new net invite count.</param>
public record InviteCountChangedEventArgs(ulong GuildId, ulong UserId, int Count);
