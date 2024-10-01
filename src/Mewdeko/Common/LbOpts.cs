using CommandLine;

namespace Mewdeko.Common;

/// <summary>
///     Represents options for the leaderboard command.
/// </summary>
public class LbOpts : IMewdekoCommandOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether to only show users who are on the server.
    /// </summary>
    [Option('c', "clean", Default = false, HelpText = "Only show users who are on the server.")]
    public bool Clean { get; set; }

    /// <summary>
    ///     Normalizes the options.
    /// </summary>
    public void NormalizeOptions()
    {
        // Method intentionally left empty.
    }
}