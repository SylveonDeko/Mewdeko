namespace Mewdeko.Common.Extensions.Interactive.Pagination
{
    /// <summary>
    ///     Specifies the paginator actions related to emotes.
    /// </summary>
    public enum PaginatorAction
    {
        /// <summary>
        ///     Go to the next page.
        /// </summary>
        Forward,

        /// <summary>
        ///     Go to the previous page.
        /// </summary>
        Backward,

        /// <summary>
        ///     Skip to the end.
        /// </summary>
        SkipToEnd,

        /// <summary>
        ///     Skip to the start.
        /// </summary>
        SkipToStart,

        /// <summary>
        ///     Exit the paginator.
        /// </summary>
        Exit
    }
}