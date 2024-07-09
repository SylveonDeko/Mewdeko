namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Specifies the action to take when a warning expires.
    /// </summary>
    public enum WarnExpireAction
    {
        /// <summary>
        /// Clear the warning.
        /// </summary>
        Clear,

        /// <summary>
        /// Delete the warning.
        /// </summary>
        Delete
    }
}