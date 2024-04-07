namespace Mewdeko.Modules.Searches.Common;

/// <summary>
/// Represents an object from the e621 API.
/// </summary>
public class E621Object
{
    /// <summary>
    /// Gets or sets the file data associated with the object.
    /// </summary>
    public FileData File { get; set; }

    /// <summary>
    /// Gets or sets the tag data associated with the object.
    /// </summary>
    public TagData Tags { get; set; }

    /// <summary>
    /// Gets or sets the score data associated with the object.
    /// </summary>
    public ScoreData Score { get; set; }

    /// <summary>
    /// Represents file data including the URL.
    /// </summary>
    public class FileData
    {
        /// <summary>
        /// Gets or sets the URL of the file.
        /// </summary>
        public string Url { get; set; }
    }

    /// <summary>
    /// Represents tag data including general tags.
    /// </summary>
    public class TagData
    {
        /// <summary>
        /// Gets or sets the general tags associated with the object.
        /// </summary>
        public string[] General { get; set; }
    }

    /// <summary>
    /// Represents score data including the total score.
    /// </summary>
    public class ScoreData
    {
        /// <summary>
        /// Gets or sets the total score of the object.
        /// </summary>
        public string Total { get; set; }
    }
}