namespace Mewdeko.Common
{
    /// <summary>
    /// Represents a provider for placeholders.
    /// </summary>
    public interface IPlaceholderProvider
    {
        /// <summary>
        /// Retrieves the list of placeholders along with their corresponding functions.
        /// </summary>
        /// <returns>The list of placeholders and their functions.</returns>
        IEnumerable<(string Name, Func<string?> Func)> GetPlaceholders();
    }
}