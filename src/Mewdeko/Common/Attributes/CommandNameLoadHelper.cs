using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mewdeko.Services.strings.impl;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Attributes;

public static class CommandNameLoadHelper
{
    private static readonly IDeserializer _deserializer
        = new Deserializer();

    private static readonly LocalBotStringsProvider strings;

    public static Lazy<Dictionary<string, string[]>> LazyCommandAliases
        = new(() => LoadCommandNames());

    public static Dictionary<string, string[]> LoadCommandNames(string aliasesFilePath = "data/aliases.yml")
    {
        var text = File.ReadAllText(aliasesFilePath);
        return _deserializer.Deserialize<Dictionary<string, string[]>>(text);
    }

    public static string[] GetAliasesFor(string methodName) =>
        LazyCommandAliases.Value.TryGetValue(methodName.ToLowerInvariant(), out var aliases) &&
        aliases.Length > 1
            ? aliases.Skip(1).ToArray()
            : Array.Empty<string>();

    public static string GetRemarksFor(string methodName) => strings.GetCommandStrings(methodName, "en-US").Desc;

    public static string GetCommandNameFor(string methodName, string description = null)
    {
        methodName = methodName.ToLowerInvariant();
        var toReturn = LazyCommandAliases.Value.TryGetValue(methodName, out var aliases) && aliases.Length > 0
            ? aliases[0]
            : methodName;
        return toReturn;
    }
}