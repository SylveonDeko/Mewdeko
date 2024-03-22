using System.IO;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
/// Helper class for loading command names and aliases.
/// </summary>
public static class CommandNameLoadHelper
{
    /// <summary>
    /// Deserializer for YAML files.
    /// </summary>
    private static readonly IDeserializer Deserializer
        = new Deserializer();

    /// <summary>
    /// Lazy-loaded dictionary of command aliases.
    /// </summary>
    public static readonly Lazy<Dictionary<string, string[]>> LazyCommandAliases
        = new(() => LoadCommandNames());

    /// <summary>
    /// Loads command names from a YAML file.
    /// </summary>
    /// <param name="aliasesFilePath">The path to the YAML file containing command aliases. Defaults to "data/aliases.yml".</param>
    /// <returns>A dictionary mapping command names to their aliases.</returns>
    public static Dictionary<string, string[]> LoadCommandNames(string aliasesFilePath = "data/aliases.yml")
    {
        var text = File.ReadAllText(aliasesFilePath);
        return Deserializer.Deserialize<Dictionary<string, string[]>>(text);
    }

    /// <summary>
    /// Gets the aliases for a given method name.
    /// </summary>
    /// <param name="methodName">The name of the method.</param>
    /// <returns>An array of aliases for the method, or an empty array if no aliases are found.</returns>
    public static string[] GetAliasesFor(string methodName) =>
        LazyCommandAliases.Value.TryGetValue(methodName.ToLowerInvariant(), out var aliases) &&
        aliases.Length > 1
            ? aliases.Skip(1).ToArray()
            : Array.Empty<string>();

    /// <summary>
    /// Gets the command name for a given method name.
    /// </summary>
    /// <param name="methodName">The name of the method.</param>
    /// <param name="description">An optional description of the command. Defaults to null.</param>
    /// <returns>The command name for the method, or the method name itself if no command name is found.</returns>
    public static string GetCommandNameFor(string methodName, string? description = null)
    {
        methodName = methodName.ToLowerInvariant();
        return LazyCommandAliases.Value.TryGetValue(methodName, out var aliases) && aliases.Length > 0
            ? aliases[0]
            : methodName;
    }
}