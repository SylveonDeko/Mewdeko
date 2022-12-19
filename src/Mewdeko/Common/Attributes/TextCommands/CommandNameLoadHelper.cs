using System.IO;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Attributes.TextCommands;

public static class CommandNameLoadHelper
{
    private static readonly IDeserializer Deserializer
        = new Deserializer();

    public static readonly Lazy<Dictionary<string, string[]>> LazyCommandAliases
        = new(() => LoadCommandNames());

    public static Dictionary<string, string[]> LoadCommandNames(string aliasesFilePath = "data/aliases.yml")
    {
        var text = File.ReadAllText(aliasesFilePath);
        return Deserializer.Deserialize<Dictionary<string, string[]>>(text);
    }

    public static string[] GetAliasesFor(string methodName) =>
        LazyCommandAliases.Value.TryGetValue(methodName.ToLowerInvariant(), out var aliases) &&
        aliases.Length > 1
            ? aliases.Skip(1).ToArray()
            : Array.Empty<string>();

    public static string GetCommandNameFor(string methodName, string? description = null)
    {
        methodName = methodName.ToLowerInvariant();
        return LazyCommandAliases.Value.TryGetValue(methodName, out var aliases) && aliases.Length > 0
            ? aliases[0]
            : methodName;
    }
}