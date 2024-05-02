using Lavalink4NET.Tracks;

namespace Mewdeko.Modules.Music.Common;

/// <summary>
/// Custom track that contains data like who requested the track and from what platform.
/// </summary>
public class MewdekoTrack
{
    /// <summary>
    /// Initializes a new instance of <see cref="MewdekoTrack"/>.
    /// </summary>
    /// <param name="index">The index of the track in the queue.</param>
    /// <param name="track">The track.</param>
    /// <param name="requester">The user who requested the track.</param>
    public MewdekoTrack(int index, LavalinkTrack track, PartialUser requester)
    {
        Index = index;
        Track = track;
        Requester = requester;
    }

    /// <summary>
    /// The index of the track in the queue.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// The track.
    /// </summary>
    public LavalinkTrack Track { get; set; }

    /// <summary>
    /// The user who requested the track.
    /// </summary>
    public PartialUser Requester { get; set; }
}

/// <summary>
/// Makes a new partial user to avoid stupid self reference loops.
/// </summary>
public class PartialUser
{
    /// <summary>
    /// The user's ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// The user's username.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// The user's avatar URL.
    /// </summary>
    public string AvatarUrl { get; set; }
}