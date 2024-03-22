namespace Mewdeko.Common
{
    /// <summary>
    /// Represents data associated with a command, including its name, description, and usage.
    /// </summary>
    public class CommandData
    {
        /// <summary>
        /// Gets or sets the name of the command.
        /// </summary>
        public string? Cmd { get; set; }

        /// <summary>
        /// Gets or sets the description of the command.
        /// </summary>
        public string? Desc { get; set; }

        /// <summary>
        /// Gets or sets an array of strings representing the usage examples of the command.
        /// </summary>
        public string[]? Usage { get; set; }
    }
}