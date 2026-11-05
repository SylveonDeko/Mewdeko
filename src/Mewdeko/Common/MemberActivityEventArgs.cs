namespace Mewdeko.Common;

/// <summary>
///     Event arguments for something a member did, published for any service that tracks it.
/// </summary>
/// <param name="GuildId">The guild it happened in, or 0 when it is not tied to one guild.</param>
/// <param name="UserId">The member.</param>
public record MemberActivityEventArgs(ulong GuildId, ulong UserId);
