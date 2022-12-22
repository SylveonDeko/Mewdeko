using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Services.strings.impl;
using NUnit.Framework;

namespace Mewdeko.Tests;

public class CommandStringsTests
{
    private const string ResponsesPath = "../../../../../src/Mewdeko/data/strings/responses";
    private const string CommandsPath = "../../../../../src/Mewdeko/data/strings/commands";
    private const string AliasesPath = "../../../../../src/Mewdeko/data/aliases.yml";

    [Test]
    public void AllCommandNamesHaveStrings()
    {
        var stringsSource = new LocalFileStringsSource(
            ResponsesPath,
            CommandsPath);
        var strings = new LocalBotStringsProvider(stringsSource);

        var culture = new CultureInfo("en-US");

        var isSuccess = true;
        foreach (var commandName in from entry in CommandNameLoadHelper.LoadCommandNames(AliasesPath)
                 select entry.Value[0]
                 into commandName
                 let cmdStrings = strings.GetCommandStrings(culture.Name, commandName)
                 where cmdStrings is null
                 select commandName)
        {
            isSuccess = false;
            TestContext.Out.WriteLine($"{commandName} doesn't exist in commands.en-US.yml");
        }

        Assert.IsTrue(isSuccess);
    }

    private static IEnumerable<string> GetCommandMethodNames()
        => typeof(Mewdeko).Assembly
            .GetExportedTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => typeof(MewdekoModule).IsAssignableFrom(type) // if its a top level module
                           || type.GetCustomAttribute<GroupAttribute>(true) is not null) // or a submodule
            .SelectMany(x => x.GetMethods()
                .Where(mi => mi.CustomAttributes
                    .Any(ca => ca.AttributeType == typeof(Cmd))))
            .Select(x => x.Name.ToLowerInvariant())
            .ToArray();

    [Test]
    public void AllCommandMethodsHaveNames()
    {
        var allAliases = CommandNameLoadHelper.LoadCommandNames(
            AliasesPath);

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
        var allAliases = CommandNameLoadHelper.LoadCommandNames(AliasesPath);

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