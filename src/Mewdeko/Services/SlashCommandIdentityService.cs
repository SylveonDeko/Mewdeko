using Discord.Commands;
using Discord.Interactions;
using ModuleInfo = Discord.Interactions.ModuleInfo;

namespace Mewdeko.Services;

/// <summary>
///     The text command identity a slash command maps to. Every permission gate keys on these values so a rule
///     written against either the text or the slash form applies to both.
/// </summary>
/// <param name="ModuleName">The top level text module name, for example Administration.</param>
/// <param name="Alias">The text command's primary alias, the key used by guild permissions and overrides.</param>
/// <param name="MethodName">The text command's method name, the key used by command cooldowns.</param>
/// <param name="IsTextCommand">Whether a matching text command was found.</param>
public sealed record SlashCommandIdentity(string ModuleName, string Alias, string MethodName, bool IsTextCommand);

/// <summary>
///     Resolves slash commands to the text command identity that the permission system is keyed on.
/// </summary>
public class SlashCommandIdentityService : INService
{
    private static readonly Dictionary<string, string> GroupToModule = new(StringComparer.OrdinalIgnoreCase)
    {
        ["protection"] = "Administration",
        ["greet"] = "Administration",
        ["roles"] = "Administration",
        ["log"] = "Administration",
        ["rolemanage"] = "ServerManagement",
        ["rolemonitor"] = "ServerManagement",
        ["channel"] = "ServerManagement",
        ["serverconfig"] = "ServerManagement",
        ["filter"] = "Permissions",
        ["remind"] = "Utility",
        ["embed"] = "Utility",
        ["repeat"] = "Utility",
        ["quote"] = "Utility",
        ["invites"] = "Utility",
        ["autopublish"] = "Utility",
        ["snipe"] = "Utility",
        ["search"] = "Searches",
        ["stream"] = "Searches",
        ["random"] = "Searches",
        ["anime"] = "Searches",
        ["rp"] = "Searches",
        ["rp2"] = "Searches",
        ["tone-tags"] = "Searches",
        ["slashtonetags"] = "Searches",
        ["tts"] = "Music",
        ["musicfx"] = "Music",
        ["fm"] = "Music",
        ["voice"] = "CustomVoice",
        ["game"] = "Games",
        ["poll"] = "PollCommands",
        ["copr"] = "CoprMonitoring",
        ["triggers"] = "ChatTriggers",
        ["rep"] = "Reputation",
        ["counting-config"] = "Counting",
        ["statchannel"] = "StatChannels",
        ["votes"] = "Vote"
    };

    private static readonly Dictionary<(string, string), string> SubgroupToModule = new()
    {
        [("counting", "moderation")] = "CountingModeration", [("owneronly", "instance")] = "InstanceManagement"
    };

    private readonly Lazy<Dictionary<string, CommandInfo>> byAlias;
    private readonly Lazy<Dictionary<string, CommandInfo>> byMethod;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SlashCommandIdentity> cache = new();
    private readonly CommandService commands;
    private readonly Lazy<Dictionary<string, string>> textModules;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SlashCommandIdentityService" /> class.
    /// </summary>
    /// <param name="commands">The text command service.</param>
    public SlashCommandIdentityService(CommandService commands)
    {
        this.commands = commands;
        byMethod = new Lazy<Dictionary<string, CommandInfo>>(BuildMethodLookup);
        byAlias = new Lazy<Dictionary<string, CommandInfo>>(BuildAliasLookup);
        textModules = new Lazy<Dictionary<string, string>>(BuildModuleLookup);
    }

    /// <summary>
    ///     Resolves the text identity of a slash or context command.
    /// </summary>
    /// <param name="command">The interaction command being executed.</param>
    /// <returns>The module name, alias, and method name the permission system should use.</returns>
    public SlashCommandIdentity Resolve(ICommandInfo command)
    {
        var (top, sub) = GetGroups(command.Module);
        var key = $"{top}/{sub}/{command.Name}/{command.MethodName}";
        return cache.GetOrAdd(key, _ => ResolveCore(command, top, sub));
    }

    /// <summary>
    ///     Resolves the text alias for a slash command path such as "xp config cooldown".
    /// </summary>
    /// <param name="interactions">The interaction service holding the registered slash commands.</param>
    /// <param name="path">The slash command path without the leading slash.</param>
    /// <returns>The identity, or null when no slash command matches the path.</returns>
    public SlashCommandIdentity? ResolvePath(InteractionService interactions, string path)
    {
        var parts = path.TrimStart('/').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        foreach (var cmd in interactions.SlashCommands)
        {
            var fullPath = GetPath(cmd);
            if (fullPath.SequenceEqual(parts, StringComparer.OrdinalIgnoreCase))
                return Resolve(cmd);
        }

        return null;
    }

    /// <summary>
    ///     Gets the full slash path of a command, for example ["xp", "config", "cooldown"].
    /// </summary>
    /// <param name="command">The slash command.</param>
    /// <returns>The path segments from the top level group down to the command name.</returns>
    public static List<string> GetPath(ICommandInfo command)
    {
        var segments = new List<string>();
        var module = command.Module;
        while (module is not null)
        {
            if (!string.IsNullOrWhiteSpace(module.SlashGroupName))
                segments.Insert(0, module.SlashGroupName);
            module = module.Parent;
        }

        segments.Add(command.Name);
        return segments;
    }

    private SlashCommandIdentity ResolveCore(ICommandInfo command, string top, string? sub)
    {
        var moduleName = ResolveModule(command, top, sub);
        CommandInfo? text = null;
        foreach (var candidate in Candidates(command, top, sub))
        {
            text = FindText(candidate);
            if (text is not null)
                break;
        }

        if (text is null)
        {
            var fallback = command.MethodName.ToLowerInvariant();
            return new SlashCommandIdentity(moduleName, fallback, fallback, false);
        }

        return new SlashCommandIdentity(moduleName, text.Name.ToLowerInvariant(),
            text.MethodName().ToLowerInvariant(), true);
    }

    private string ResolveModule(ICommandInfo command, string top, string? sub)
    {
        if (sub is not null && SubgroupToModule.TryGetValue((top.ToLowerInvariant(), sub.ToLowerInvariant()),
                out var subModule))
            return subModule;

        if (GroupToModule.TryGetValue(top, out var mapped))
            return mapped;

        if (textModules.Value.TryGetValue(Normalize(top), out var direct))
            return direct;

        return string.IsNullOrWhiteSpace(top) ? command.Module.Name : top;
    }

    private static IEnumerable<string> Candidates(ICommandInfo command, string top, string? sub)
    {
        var method = command.MethodName;
        var slash = command.Name;
        yield return method;
        yield return top + method;
        if (sub is not null)
        {
            yield return sub + method;
            yield return top + sub + method;
        }

        yield return top + slash;
        if (sub is not null)
        {
            yield return sub + slash;
            yield return top + sub + slash;
        }

        yield return slash;
    }

    private CommandInfo? FindText(string candidate)
    {
        var key = Normalize(candidate);
        if (byMethod.Value.TryGetValue(key, out var byName))
            return byName;
        return byAlias.Value.TryGetValue(key, out var byAliasHit) ? byAliasHit : null;
    }

    private static (string Top, string? Sub) GetGroups(ModuleInfo module)
    {
        var groups = new List<string>();
        var current = module;
        while (current is not null)
        {
            if (!string.IsNullOrWhiteSpace(current.SlashGroupName))
                groups.Insert(0, current.SlashGroupName);
            current = current.Parent;
        }

        if (groups.Count == 0)
            return (module.Name, null);

        return (groups[0], groups.Count > 1 ? groups[1] : null);
    }

    private static string Normalize(string value)
    {
        return value.Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
    }

    private Dictionary<string, CommandInfo> BuildMethodLookup()
    {
        var dict = new Dictionary<string, CommandInfo>();
        foreach (var cmd in commands.Commands)
            dict.TryAdd(Normalize(cmd.MethodName()), cmd);
        return dict;
    }

    private Dictionary<string, CommandInfo> BuildAliasLookup()
    {
        var dict = new Dictionary<string, CommandInfo>();
        foreach (var cmd in commands.Commands)
        {
            foreach (var alias in cmd.Aliases)
                dict.TryAdd(Normalize(alias), cmd);
        }

        return dict;
    }

    private Dictionary<string, string> BuildModuleLookup()
    {
        var dict = new Dictionary<string, string>();
        foreach (var module in commands.Modules)
        {
            var top = module.GetTopLevelModule();
            dict.TryAdd(Normalize(top.Name), top.Name);
        }

        return dict;
    }
}