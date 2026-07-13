using System.Reflection;
using Mewdeko.Modules.Twitch.Common;
using Mewdeko.Services.strings;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Modules.Twitch.Services;

/// <summary>
///     Minimum permission level required to invoke a Twitch chat command.
///     Applied via <see cref="TwitchCommandAttribute" />.
/// </summary>
public enum TwitchCommandPermission
{
    /// <summary>Any viewer in chat can run this command.</summary>
    Everyone = 0,

    /// <summary>Subscribers and above can run this command.</summary>
    Subscriber = 1,

    /// <summary>VIPs and above can run this command.</summary>
    Vip = 2,

    /// <summary>Moderators and the broadcaster can run this command.</summary>
    Mod = 3,

    /// <summary>Only the broadcaster can run this command.</summary>
    Broadcaster = 4
}

/// <summary>
///     Marks a method as a Twitch chat command handler. The method must be public, return
///     <see cref="Task" />, and take no parameters (context is available via the module base class).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TwitchCommandAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new <see cref="TwitchCommandAttribute" />.
    /// </summary>
    /// <param name="name">The command name without the prefix, e.g. <c>ping</c>.</param>
    /// <param name="permission">The minimum permission level required to invoke this command.</param>
    public TwitchCommandAttribute(string name, TwitchCommandPermission permission = TwitchCommandPermission.Everyone)
    {
        Name = name.ToLowerInvariant();
        Permission = permission;
    }

    /// <summary>Gets the command name in lowercase, without the prefix.</summary>
    public string Name { get; }

    /// <summary>Gets the minimum permission level required to invoke this command.</summary>
    public TwitchCommandPermission Permission { get; }
}

/// <summary>
///     Scans registered modules for <see cref="TwitchCommandAttribute" />-decorated methods and dispatches
///     incoming Twitch chat messages to the appropriate handler. Analogous to Discord.Net's
///     <c>CommandService</c>, but for Twitch IRC chat.
/// </summary>
public class TwitchCommandHandler : INService
{
    private readonly ConcurrentDictionary<string, CommandEntry> commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<TwitchCommandHandler> logger;
    private readonly IServiceProvider services;

    /// <summary>
    ///     Initializes a new <see cref="TwitchCommandHandler" /> and auto-registers all
    ///     <see cref="TwitchModuleBase" /> subclasses found in the executing assembly.
    /// </summary>
    /// <param name="logger">Logger for this handler.</param>
    /// <param name="services">Service provider used to resolve and construct module instances.</param>
    public TwitchCommandHandler(ILogger<TwitchCommandHandler> logger, IServiceProvider services)
    {
        this.logger = logger;
        this.services = services;
        RegisterModulesFromAssembly(typeof(TwitchCommandHandler).Assembly);
    }

    /// <summary>
    ///     Scans an assembly for <see cref="TwitchModuleBase" /> subclasses and registers all methods
    ///     decorated with <see cref="TwitchCommandAttribute" />.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    public void RegisterModulesFromAssembly(Assembly assembly)
    {
        var moduleTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(TwitchModuleBase)));

        foreach (var type in moduleTypes)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = method.GetCustomAttribute<TwitchCommandAttribute>();
                if (attr is null) continue;

                commands[attr.Name] = new CommandEntry(type, method, attr.Permission);
                logger.LogDebug("Registered Twitch command !{Name} -> {Type}.{Method}",
                    attr.Name, type.Name, method.Name);
            }
        }

        logger.LogInformation("Registered {Count} Twitch command(s)", commands.Count);
    }

    /// <summary>
    ///     Returns the name and required permission level of every registered Twitch chat command,
    ///     sorted alphabetically. Used to surface the command list on the dashboard.
    /// </summary>
    public IReadOnlyList<(string Name, TwitchCommandPermission Permission)> GetRegisteredCommands()
    {
        return commands
            .Select(kv => (kv.Key, kv.Value.Permission))
            .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    ///     Parses the command name from the context message, checks permissions, constructs the module
    ///     with the context already set, and invokes the handler. Silently ignores unknown commands.
    /// </summary>
    /// <param name="ctx">The Twitch command context built from the incoming chat message.</param>
    public async Task ExecuteAsync(TwitchCommandContext ctx)
    {
        var text = ctx.MessageText[ctx.CommandPrefix.Length..].TrimStart();
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var name = parts[0].ToLowerInvariant();
        ctx.Args = parts[1..];

        if (!commands.TryGetValue(name, out var entry))
        {
            var twitchService = services.GetRequiredService<TwitchService>();
            await twitchService.TryExecuteCustomCommandAsync(ctx, name);
            return;
        }

        if ((int)ctx.PermissionLevel < (int)entry.Permission)
        {
            logger.LogDebug("Twitch user {User} lacks permission for !{Command}", ctx.Username, name);
            return;
        }

        try
        {
            var module = (TwitchModuleBase)ActivatorUtilities.CreateInstance(services, entry.ModuleType);
            module.Context = ctx;
            module.Strings = services.GetRequiredService<IBotStrings>();
            module.TwitchSvc = services.GetRequiredService<TwitchService>();

            // Populate the typed Service property for TwitchModuleBase<TService> subclasses.
            var baseType = entry.ModuleType.BaseType;
            if (baseType is { IsGenericType: true } &&
                baseType.GetGenericTypeDefinition() == typeof(TwitchModuleBase<>))
            {
                var serviceType = baseType.GetGenericArguments()[0];
                entry.ModuleType.GetProperty("Service")?.SetValue(module, services.GetRequiredService(serviceType));
            }

            var result = entry.Method.Invoke(module, null);
            if (result is Task task)
                await task;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing Twitch command !{Command} invoked by {User}", name, ctx.Username);
        }
    }

    private sealed class CommandEntry(Type moduleType, MethodInfo method, TwitchCommandPermission permission)
    {
        /// <summary>Gets the module type that owns this command.</summary>
        public Type ModuleType { get; } = moduleType;

        /// <summary>Gets the reflected method to invoke.</summary>
        public MethodInfo Method { get; } = method;

        /// <summary>Gets the minimum permission level required to invoke this command.</summary>
        public TwitchCommandPermission Permission { get; } = permission;
    }
}