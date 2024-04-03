namespace Mewdeko.Modules.Nsfw.Common.Downloaders
{
    /// <summary>
    /// Represents the response object from the E621 API.
    /// </summary>
    public class E621Response
    {
        /// <summary>
        /// Gets or sets the list of E621 posts.
        /// </summary>
        public List<E621Object> Posts { get; set; }
    }
}