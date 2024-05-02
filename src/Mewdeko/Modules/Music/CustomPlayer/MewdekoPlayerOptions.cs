using Lavalink4NET.Players;

namespace Mewdeko.Modules.Music.CustomPlayer;

/// <summary>
/// Custom LavaLink player options such as the text channel the player was started in.
/// </summary>
public record MewdekoPlayerOptions : LavalinkPlayerOptions
{
    /// <summary>
    /// The text channel the player was started in.
    /// </summary>
    public IMessageChannel Channel { get; init; }
}