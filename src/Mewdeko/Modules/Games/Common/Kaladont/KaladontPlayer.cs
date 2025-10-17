namespace Mewdeko.Modules.Games.Common.Kaladont;

/// <summary>
///     Represents a player in a Kaladont game.
/// </summary>
/// <param name="UserId">The Discord user ID of the player.</param>
/// <param name="UserName">The Discord username of the player.</param>
public record KaladontPlayer(ulong UserId, string UserName);