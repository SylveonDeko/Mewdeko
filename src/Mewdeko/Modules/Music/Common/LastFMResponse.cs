using Newtonsoft.Json;

namespace Mewdeko.Modules.Music.Common;

/// <summary>
/// Represents an artist with details like name, MusicBrainz ID, and their URL on Last.fm.
/// </summary>
public class Artist
{
    /// <summary>
    /// Gets or sets the MusicBrainz ID of the artist.
    /// </summary>
    [JsonProperty("mbid")] public string Mbid;

    /// <summary>
    /// Gets or sets the name of the artist.
    /// </summary>
    [JsonProperty("name")] public string Name;

    /// <summary>
    /// Gets or sets the URL of the Last.fm page for the artist.
    /// </summary>
    [JsonProperty("url")] public string Url;
}

/// <summary>
/// Attribute data related to the similar tracks response.
/// </summary>
public class Attr
{
    /// <summary>
    /// Gets or sets the artist name associated with the similar tracks.
    /// </summary>
    [JsonProperty("artist")] public string Artist;
}

/// <summary>
/// Represents an image with a URL and its size.
/// </summary>
public class Image
{
    /// <summary>
    /// Gets or sets the size of the image (e.g., small, medium, large).
    /// </summary>
    [JsonProperty("size")] public string Size;

    /// <summary>
    /// Gets or sets the URL of the image.
    /// </summary>
    [JsonProperty("#text")] public string Text;
}

/// <summary>
/// The root response object for LastFM API calls returning similar tracks.
/// </summary>
public class LastFmResponse
{
    /// <summary>
    /// Gets or sets the similar tracks returned by the API.
    /// </summary>
    [JsonProperty("similartracks")] public Similartracks? Similartracks;
}

/// <summary>
/// Contains a list of similar tracks and associated attributes.
/// </summary>
public class Similartracks
{
    /// <summary>
    /// Gets or sets additional attributes for the similar tracks.
    /// </summary>
    [JsonProperty("@attr")] public Attr Attr;

    /// <summary>
    /// Gets or sets the list of similar tracks.
    /// </summary>
    [JsonProperty("track")] public List<Track> Track;
}

/// <summary>
/// Represents the streamable status of a track.
/// </summary>
public class Streamable
{
    /// <summary>
    /// Gets or sets the flag indicating if the full track is streamable.
    /// </summary>
    [JsonProperty("fulltrack")] public string Fulltrack;

    /// <summary>
    /// Gets or sets the text indicating whether a track is streamable.
    /// </summary>
    [JsonProperty("#text")] public string Text;
}

/// <summary>
/// Represents a music track with various properties like name, play count, and URL.
/// </summary>
public class Track
{
    /// <summary>
    /// Gets or sets the artist of the track.
    /// </summary>
    [JsonProperty("artist")] public Artist Artist;

    /// <summary>
    /// Gets or sets the duration of the track in milliseconds.
    /// </summary>
    [JsonProperty("duration")] public int? Duration;

    /// <summary>
    /// Gets or sets the list of images associated with the track.
    /// </summary>
    [JsonProperty("image")] public List<Image> Image;

    /// <summary>
    /// Gets or sets the match score indicating the relevance of the track in the context of the search.
    /// </summary>
    [JsonProperty("match")] public double? Match;

    /// <summary>
    /// Gets or sets the MusicBrainz ID of the track.
    /// </summary>
    [JsonProperty("mbid")] public string Mbid;

    /// <summary>
    /// Gets or sets the name of the track.
    /// </summary>
    [JsonProperty("name")] public string Name;

    /// <summary>
    /// Gets or sets the play count of the track.
    /// </summary>
    [JsonProperty("playcount")] public int? Playcount;

    /// <summary>
    /// Gets or sets the streamable status of the track.
    /// </summary>
    [JsonProperty("streamable")] public Streamable Streamable;

    /// <summary>
    /// Gets or sets the URL of the Last.fm page for the track.
    /// </summary>
    [JsonProperty("url")] public string Url;
}