namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents information about a movie obtained from the OMDB API.
/// </summary>
public class OmdbMovie
{
    /// <summary>
    ///     Gets or sets the title of the movie.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    ///     Gets or sets the year the movie was released.
    /// </summary>
    public string Year { get; set; }

    /// <summary>
    ///     Gets or sets the IMDb rating of the movie.
    /// </summary>
    public string ImdbRating { get; set; }

    /// <summary>
    ///     Gets or sets the IMDb ID of the movie.
    /// </summary>
    public string ImdbId { get; set; }

    /// <summary>
    ///     Gets or sets the genre(s) of the movie.
    /// </summary>
    public string Genre { get; set; }

    /// <summary>
    ///     Gets or sets the plot summary of the movie.
    /// </summary>
    public string Plot { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the movie poster.
    /// </summary>
    public string Poster { get; set; }
}