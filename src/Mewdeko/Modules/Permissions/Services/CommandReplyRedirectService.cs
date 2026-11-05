using DataModel;
using Discord.Commands;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Permissions.Services;

/// <summary>
///     Sends the replies of chosen commands or modules to a set channel instead of where they were used.
/// </summary>
public class CommandReplyRedirectService(IDataConnectionFactory dbFactory, CommandService commands) : INService
{
    private readonly ConcurrentDictionary<ulong, List<CommandReplyRedirect>> cache = new();

    /// <summary>
    ///     Describes one redirect rule on a single line.
    /// </summary>
    /// <param name="strings">The localized strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>The line.</returns>
    public static string Describe(GeneratedBotStrings strings, ulong guildId, CommandReplyRedirect rule)
    {
        var line = rule.IsModule
            ? strings.CmdReplyLineModule(guildId, rule.Target, rule.ChannelId)
            : strings.CmdReplyLineCommand(guildId, rule.Target, rule.ChannelId);
        return rule.DeleteInvoker ? $"{line} {strings.CmdReplyDeletes(guildId)}" : line;
    }

    /// <summary>
    ///     Finds the command or module a name refers to.
    /// </summary>
    /// <param name="input">A command name, alias or module name.</param>
    /// <returns>The rule target and whether it is a module, or null when nothing matches.</returns>
    public (string Target, bool IsModule)? ResolveTarget(string input)
    {
        var name = input.Trim().ToLowerInvariant();
        var command = commands.Commands.FirstOrDefault(c =>
            c.MethodName().Equals(name, StringComparison.OrdinalIgnoreCase) ||
            c.Aliases.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase)));
        if (command != null)
            return (command.MethodName().ToLowerInvariant(), false);

        var module = commands.Modules.Select(m => m.GetTopLevelModule())
            .FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return module == null ? null : (module.Name.ToLowerInvariant(), true);
    }

    /// <summary>
    ///     Gets a guild's redirect rules.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The rules, commands first.</returns>
    public async Task<List<CommandReplyRedirect>> GetRulesAsync(ulong guildId)
    {
        if (cache.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var rules = await db.CommandReplyRedirects.Where(r => r.GuildId == guildId)
            .OrderBy(r => r.IsModule).ThenBy(r => r.Target).ToListAsync();
        cache[guildId] = rules;
        return rules;
    }

    /// <summary>
    ///     Adds or replaces a redirect rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">The lowercase command or module name.</param>
    /// <param name="isModule">Whether the target is a module.</param>
    /// <param name="channelId">The channel replies go to.</param>
    /// <param name="deleteInvoker">Whether the invoking message is deleted.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetAsync(ulong guildId, string target, bool isModule, ulong channelId, bool deleteInvoker)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var updated = await db.CommandReplyRedirects
            .Where(r => r.GuildId == guildId && r.IsModule == isModule && r.Target == target)
            .Set(r => r.ChannelId, channelId)
            .Set(r => r.DeleteInvoker, deleteInvoker)
            .UpdateAsync();

        if (updated == 0)
        {
            await db.InsertAsync(new CommandReplyRedirect
            {
                GuildId = guildId,
                Target = target,
                IsModule = isModule,
                ChannelId = channelId,
                DeleteInvoker = deleteInvoker
            });
        }

        cache.TryRemove(guildId, out _);
    }

    /// <summary>
    ///     Removes a redirect rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">The lowercase command or module name.</param>
    /// <param name="isModule">Whether the target is a module.</param>
    /// <returns>Whether a rule was removed.</returns>
    public async Task<bool> RemoveAsync(ulong guildId, string target, bool isModule)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var removed = await db.CommandReplyRedirects
            .Where(r => r.GuildId == guildId && r.IsModule == isModule && r.Target == target)
            .DeleteAsync();
        cache.TryRemove(guildId, out _);
        return removed > 0;
    }

    /// <summary>
    ///     Picks the context a command should run with. Commands run in a context that replies elsewhere when a rule
    ///     matches and the bot can post in the rule's channel.
    /// </summary>
    /// <param name="context">The original context.</param>
    /// <param name="command">The command about to run.</param>
    /// <returns>The context to run with and whether the invoking message should be deleted afterwards.</returns>
    public async Task<(ICommandContext Context, bool DeleteInvoker)> ApplyAsync(ICommandContext context,
        CommandInfo command)
    {
        if (context.Guild == null)
            return (context, false);

        var rules = await GetRulesAsync(context.Guild.Id);
        if (rules.Count == 0)
            return (context, false);

        var commandName = command.MethodName().ToLowerInvariant();
        var moduleName = command.Module.GetTopLevelModule().Name.ToLowerInvariant();
        var rule = rules.FirstOrDefault(r => !r.IsModule && r.Target == commandName)
                   ?? rules.FirstOrDefault(r => r.IsModule && r.Target == moduleName);
        if (rule == null)
            return (context, false);

        var channel = await context.Guild.GetTextChannelAsync(rule.ChannelId);
        var bot = await context.Guild.GetCurrentUserAsync();
        if (channel == null || !bot.GetPermissions(channel).SendMessages)
            return (context, false);

        var redirected = channel.Id == context.Channel.Id ? context : new RedirectedCommandContext(context, channel);
        return (redirected, rule.DeleteInvoker);
    }
}

/// <summary>
///     A command context whose replies go to a different channel.
/// </summary>
public class RedirectedCommandContext(ICommandContext inner, IMessageChannel channel) : ICommandContext
{
    /// <inheritdoc />
    public IDiscordClient Client => inner.Client;

    /// <inheritdoc />
    public IGuild Guild => inner.Guild;

    /// <inheritdoc />
    public IMessageChannel Channel => channel;

    /// <inheritdoc />
    public IUser User => inner.User;

    /// <inheritdoc />
    public IUserMessage Message => inner.Message;
}
