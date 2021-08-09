using System;

namespace Mewdeko.Interactive.Pagination
{
    /// <summary>
    /// Specifies which contents should be displayed in the footer of a <see cref="PaginatorBuilder{TPaginator, TBuilder}"/>.
    /// </summary>
    [Flags]
    public enum PaginatorFooter
    {
        /// <summary>
        /// Display nothing in the footer.
        /// </summary>
        None = 0,
        /// <summary>
        /// Displays the current page number in the footer.
        /// </summary>
        PageNumber = 1 << 0,
        /// <summary>
        /// Displays the users who can interact with the paginator.
        /// </summary>
        Users = 1 << 1
    }
}