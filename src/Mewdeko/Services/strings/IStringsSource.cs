using Mewdeko.Services.strings.impl;

namespace Mewdeko.Services.strings;

/// <summary>
///     Basic interface used for classes implementing strings loading mechanism.
/// </summary>
public interface IStringsSource
{
    /// <summary>
    ///     Gets all response strings.
    /// </summary>
    /// <returns>A dictionary containing response strings indexed by locale name and then by key.</returns>
    public Dictionary<string, Dictionary<string, string>> GetResponseStrings();

    /// <summary>
    ///     Gets the response strings file by file, in the order they are merged: within a locale, later files win.
    /// </summary>
    /// <returns>One entry per source file.</returns>
    public List<ResponseFile> GetResponseFiles();

    /// <summary>
    ///     Gets all command strings.
    /// </summary>
    /// <returns>A dictionary containing command strings indexed by locale name and then by command name.</returns>
    public Dictionary<string, Dictionary<string, CommandStrings>> GetCommandStrings();
}