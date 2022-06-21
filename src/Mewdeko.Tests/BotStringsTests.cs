using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Services.strings.impl;
using NUnit.Framework;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Mewdeko.Tests;

public class CommandStringsTests
{
    private const string RESPONSES_PATH = "../../../../../src/Mewdeko/data/strings/responses";
    private const string COMMANDS_PATH = "../../../../../src/Mewdeko/data/strings/commands";
    private const string ALIASES_PATH = "../../../../../src/Mewdeko/data/aliases.yml";

    [Test]
    public void AllCommandNamesHaveStrings()
    {
        var stringsSource = new LocalFileStringsSource(
            RESPONSES_PATH,
            COMMANDS_PATH);
        var strings = new LocalBotStringsProvider(stringsSource);

        var culture = new CultureInfo("en-US");

        var isSuccess = true;
        foreach (var entry in CommandNameLoadHelper.LoadCommandNames(ALIASES_PATH))
        {
            var commandName = entry.Value[0];

            var cmdStrings = strings.GetCommandStrings(culture.Name, commandName);
            if (cmdStrings is not null) continue;
            isSuccess = false;
            TestContext.Out.WriteLine($"{commandName} doesn't exist in commands.en-US.yml");
        }
        Assert.IsTrue(isSuccess);
    }

    private static string[] GetCommandMethodNames()
        => typeof(Mewdeko).Assembly
                          .GetExportedTypes()
                          .Where(type => type.IsClass && !type.IsAbstract)
                          .Where(type => typeof(MewdekoModule).IsAssignableFrom(type) // if its a top level module
                                         || !(type.GetCustomAttribute<GroupAttribute>(true) is null)) // or a submodule
                          .SelectMany(x => x.GetMethods()
                                            .Where(mi => mi.CustomAttributes
                                                           .Any(ca => ca.AttributeType == typeof(Cmd))))
                          .Select(x => x.Name.ToLowerInvariant())
                          .ToArray();

    [Test]
    public void AllCommandMethodsHaveNames()
    {
        var allAliases = CommandNameLoadHelper.LoadCommandNames(
            ALIASES_PATH);

        var methodNames = GetCommandMethodNames();

        var isSuccess = true;
        foreach (var methodName in methodNames)
        {
            if (allAliases.TryGetValue(methodName, out _)) continue;
            TestContext.Error.WriteLine($"{methodName} is missing an alias.");
            isSuccess = false;
        }

        Assert.IsTrue(isSuccess);
    }

    [Test]
    public void NoObsoleteAliases()
    {
        var allAliases = CommandNameLoadHelper.LoadCommandNames(ALIASES_PATH);

        var methodNames = GetCommandMethodNames()
            .ToHashSet();

        var isSuccess = true;

        foreach (var methodName in allAliases.Select(item => item.Key).Where(methodName => !methodNames.Contains(methodName)))
        {
            TestContext.WriteLine($"'{methodName}' from aliases.yml doesn't have a matching command method.");
            isSuccess = false;
        }

        Assert.IsTrue(isSuccess);
    }
}