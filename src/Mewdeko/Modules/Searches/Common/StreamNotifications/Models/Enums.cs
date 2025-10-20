namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
///     Specifies the type of the stream.
/// </summary>
public enum FType
{
    /// <summary>
    ///     Twitch stream.
    /// </summary>
    Twitch = 0,

    /// <summary>
    ///     Picarto stream.
    /// </summary>
    Picarto = 3,

    /// <summary>
    ///     YouTube stream.
    /// </summary>
    Youtube = 4,

    /// <summary>
    ///     Facebook stream.
    /// </summary>
    Facebook = 5,

    /// <summary>
    ///     Trovo stream.
    /// </summary>
    Trovo = 6,

    /// <summary>
    ///     Kick stream.
    /// </summary>
    Kick = 7
}