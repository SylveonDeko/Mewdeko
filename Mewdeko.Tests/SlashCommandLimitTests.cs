using System.Reflection;
using Discord.Interactions;
using Mewdeko.Common.Attributes.TextCommands;

namespace Mewdeko.Tests;

/// <summary>
///     Guards the Discord limit of 25 options per slash command or group.
/// </summary>
[TestFixture]
public class SlashCommandLimitTests
{
    private const int MaxOptions = 25;

    /// <summary>
    ///     No slash group may hold more than 25 subcommands and subgroups, and no command more than 25 parameters.
    /// </summary>
    [Test]
    public void SlashGroupsAndCommandsStayWithinOptionLimit()
    {
        var failures = new List<string>();
        var types = typeof(Cmd).Assembly.GetTypes();

        foreach (var type in types.Where(t => t.GetCustomAttribute<GroupAttribute>(false) != null))
        {
            var count = CountOptions(type);
            if (count > MaxOptions)
                failures.Add($"Group '{type.GetCustomAttribute<GroupAttribute>(false)!.Name}' ({type.FullName}) has {count} options");
        }

        foreach (var method in types.SelectMany(t =>
                     t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)))
        {
            if (method.GetCustomAttribute<SlashCommandAttribute>() == null) continue;
            var parameters = method.GetParameters().Length;
            if (parameters > MaxOptions)
                failures.Add($"Command '{method.DeclaringType?.Name}.{method.Name}' has {parameters} parameters");
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    /// <summary>
    ///     Discord allows at most 100 top-level chat input commands.
    /// </summary>
    [Test]
    public void TopLevelCommandsStayWithinLimit()
    {
        var topLevel = typeof(Cmd).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsNested && typeof(IInteractionModuleBase).IsAssignableFrom(t))
            .Sum(t => t.GetCustomAttribute<GroupAttribute>(false) != null ? 1 : CountOptions(t));

        Assert.That(topLevel, Is.LessThanOrEqualTo(100));
    }

    /// <summary>
    ///     Top-level command names must be unique.
    /// </summary>
    [Test]
    public void TopLevelNamesAreUnique()
    {
        var names = typeof(Cmd).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsNested && typeof(IInteractionModuleBase).IsAssignableFrom(t))
            .SelectMany(TopLevelNames)
            .ToList();

        var duplicates = names.GroupBy(n => n.Name).Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}': {string.Join(", ", g.Select(n => n.Source))}")
            .ToList();

        Assert.That(duplicates, Is.Empty, string.Join("\n", duplicates));
    }

    private static IEnumerable<(string Name, string Source)> TopLevelNames(Type type)
    {
        if (type.GetCustomAttribute<GroupAttribute>(false) is { } group)
        {
            yield return (group.Name, type.FullName!);
            yield break;
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            if (method.GetCustomAttribute<SlashCommandAttribute>() is { } cmd)
                yield return (cmd.Name, $"{type.FullName}.{method.Name}");

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(n => typeof(IInteractionModuleBase).IsAssignableFrom(n)))
            foreach (var name in TopLevelNames(nested))
                yield return name;
    }

    private static int CountOptions(Type type)
    {
        var commands = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(m => m.GetCustomAttribute<SlashCommandAttribute>() != null);

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!typeof(IInteractionModuleBase).IsAssignableFrom(nested)) continue;
            commands += nested.GetCustomAttribute<GroupAttribute>(false) != null ? 1 : CountOptions(nested);
        }

        return commands;
    }
}
