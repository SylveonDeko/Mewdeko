using Discord.Interactions;
using Mewdeko.Modules.Chat_Triggers.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     What a slash command check found for a guild.
/// </summary>
/// <param name="GlobalExpected">How many global commands the bot should have.</param>
/// <param name="GlobalRegistered">How many global commands Discord has.</param>
/// <param name="GlobalMissing">Global commands the bot has but Discord does not.</param>
/// <param name="GlobalExtra">Global commands Discord has but the bot no longer has.</param>
/// <param name="GuildExpected">How many guild commands the guild's chat triggers should have.</param>
/// <param name="GuildRegistered">How many guild commands Discord has for the guild.</param>
/// <param name="GuildMissing">Chat trigger commands Discord does not have for the guild.</param>
/// <param name="GuildExtra">Guild commands Discord has that no chat trigger accounts for.</param>
/// <param name="MissingBotPermissions">Permissions the bot lacks in the checked channel.</param>
/// <param name="UserCanUseCommands">Whether the member checked can use application commands in the channel.</param>
/// <param name="EveryoneCanUseCommands">Whether everyone can use application commands in the channel.</param>
/// <param name="Reachable">Whether Discord's command lists could be read.</param>
public record SlashDiagnosis(
    int GlobalExpected,
    int GlobalRegistered,
    IReadOnlyList<string> GlobalMissing,
    IReadOnlyList<string> GlobalExtra,
    int GuildExpected,
    int GuildRegistered,
    IReadOnlyList<string> GuildMissing,
    IReadOnlyList<string> GuildExtra,
    IReadOnlyList<string> MissingBotPermissions,
    bool? UserCanUseCommands,
    bool? EveryoneCanUseCommands,
    bool Reachable);

/// <summary>
///     Compares the slash commands Discord has for a guild with the ones the bot expects, and re-registers the
///     guild's commands.
/// </summary>
/// <param name="client">The Discord client.</param>
/// <param name="interactions">The interaction service holding the bot's slash commands.</param>
/// <param name="chatTriggers">The chat trigger service that owns each guild's own slash commands.</param>
/// <param name="logger">The logger.</param>
public class SlashDiagnosticsService(
    DiscordShardedClient client,
    InteractionService interactions,
    ChatTriggersService chatTriggers,
    ILogger<SlashDiagnosticsService> logger) : INService
{
    private static readonly (ChannelPermission Permission, string Name)[] BotNeeds =
    [
        (ChannelPermission.ViewChannel, "View Channel"),
        (ChannelPermission.SendMessages, "Send Messages"),
        (ChannelPermission.EmbedLinks, "Embed Links")
    ];

    /// <summary>
    ///     Checks a guild's slash commands against what the bot expects.
    /// </summary>
    /// <param name="guild">The guild to check.</param>
    /// <param name="channel">The channel to check permissions in, if any.</param>
    /// <param name="member">The member whose access to check, if any.</param>
    /// <returns>What the check found.</returns>
    public async Task<SlashDiagnosis> DiagnoseAsync(IGuild guild, IGuildChannel? channel = null,
        IGuildUser? member = null)
    {
        var expectedGlobal = interactions.SlashCommands
            .Select(x => TopLevelName(x))
            .Concat(interactions.ContextCommands.Select(x => x.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expectedGuild = new List<string>();
        var (ok, props) = await chatTriggers.TryGetApplicationCommandProperties(guild.Id).ConfigureAwait(false);
        if (ok && props is not null)
            expectedGuild = props.Select(x => x.Name.IsSpecified ? x.Name.Value : "").Where(x => x != "").ToList();

        var reachable = true;
        var registeredGlobal = new List<string>();
        var registeredGuild = new List<string>();
        try
        {
            registeredGlobal = (await client.Rest.GetGlobalApplicationCommands().ConfigureAwait(false))
                .Select(x => x.Name).ToList();
            registeredGuild = (await client.Rest.GetGuildApplicationCommands(guild.Id).ConfigureAwait(false))
                .Select(x => x.Name).ToList();
        }
        catch (Exception ex)
        {
            reachable = false;
            logger.LogDebug(ex, "Could not read registered slash commands for guild {GuildId}", guild.Id);
        }

        var (missingPermissions, userCan, everyoneCan) = await CheckPermissionsAsync(guild, channel, member)
            .ConfigureAwait(false);

        return new SlashDiagnosis(
            expectedGlobal.Count,
            registeredGlobal.Count,
            reachable ? Missing(expectedGlobal, registeredGlobal) : [],
            reachable ? Missing(registeredGlobal, expectedGlobal) : [],
            expectedGuild.Count,
            registeredGuild.Count,
            reachable ? Missing(expectedGuild, registeredGuild) : [],
            reachable ? Missing(registeredGuild, expectedGuild) : [],
            missingPermissions,
            userCan,
            everyoneCan,
            reachable);
    }

    /// <summary>
    ///     Registers the guild's own slash commands with Discord again.
    /// </summary>
    /// <param name="guild">The guild to register commands for.</param>
    /// <returns>True when the registration ran.</returns>
    public async Task<bool> ResyncAsync(IGuild guild)
    {
        try
        {
            await chatTriggers.RegisterTriggersToGuildAsync(guild).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not re-register slash commands for guild {GuildId}", guild.Id);
            return false;
        }
    }

    /// <summary>
    ///     Builds the embed that reports a check.
    /// </summary>
    /// <param name="strings">The response strings.</param>
    /// <param name="guildId">The guild the report is for.</param>
    /// <param name="diagnosis">What the check found.</param>
    /// <returns>The report embed.</returns>
    public static EmbedBuilder BuildEmbed(GeneratedBotStrings strings, ulong guildId, SlashDiagnosis diagnosis)
    {
        var eb = new EmbedBuilder().WithOkColor(guildId).WithTitle(strings.SlashDiagTitle(guildId));

        if (!diagnosis.Reachable)
        {
            eb.WithDescription(strings.SlashDiagUnreachable(guildId));
            return eb.WithErrorColor();
        }

        eb.AddField(strings.SlashDiagGlobal(guildId),
            strings.SlashDiagCounts(guildId, diagnosis.GlobalRegistered, diagnosis.GlobalExpected));
        AddList(eb, strings.SlashDiagGlobalMissing(guildId), diagnosis.GlobalMissing);
        AddList(eb, strings.SlashDiagGlobalExtra(guildId), diagnosis.GlobalExtra);

        eb.AddField(strings.SlashDiagGuild(guildId),
            strings.SlashDiagCounts(guildId, diagnosis.GuildRegistered, diagnosis.GuildExpected));
        AddList(eb, strings.SlashDiagGuildMissing(guildId), diagnosis.GuildMissing);
        AddList(eb, strings.SlashDiagGuildExtra(guildId), diagnosis.GuildExtra);

        if (diagnosis.MissingBotPermissions.Count > 0)
            eb.AddField(strings.SlashDiagBotMissing(guildId), string.Join(", ", diagnosis.MissingBotPermissions));

        if (diagnosis.UserCanUseCommands == false)
            eb.AddField(strings.SlashDiagUserBlocked(guildId), "​");

        if (diagnosis.EveryoneCanUseCommands == false)
            eb.AddField(strings.SlashDiagEveryoneBlocked(guildId), "​");

        var healthy = diagnosis.GlobalMissing.Count == 0 && diagnosis.GuildMissing.Count == 0 &&
                      diagnosis.MissingBotPermissions.Count == 0 && diagnosis.UserCanUseCommands != false &&
                      diagnosis.EveryoneCanUseCommands != false;
        eb.WithFooter(healthy ? strings.SlashDiagHealthy(guildId) : strings.SlashDiagResyncHint(guildId));
        return eb;
    }

    private static void AddList(EmbedBuilder eb, string name, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
            return;

        var shown = items.Take(15).ToList();
        var text = string.Join(", ", shown.Select(x => $"`{x}`"));
        if (items.Count > shown.Count)
            text += $" +{items.Count - shown.Count}";

        eb.AddField(name, text.TrimTo(1024));
    }

    private static string TopLevelName(SlashCommandInfo command)
    {
        var module = command.Module;
        while (module.Parent is not null)
            module = module.Parent;

        return module.IsSlashGroup ? module.SlashGroupName : command.Name;
    }

    private static List<string> Missing(IEnumerable<string> have, IEnumerable<string> against)
    {
        var other = against.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return have.Where(x => !other.Contains(x)).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
    }

    private static async Task<(List<string> MissingBot, bool? User, bool? Everyone)> CheckPermissionsAsync(IGuild guild,
        IGuildChannel? channel, IGuildUser? member)
    {
        var missing = new List<string>();
        if (channel is null)
            return (missing, null, null);

        var bot = await guild.GetCurrentUserAsync().ConfigureAwait(false);
        var botPermissions = bot.GetPermissions(channel);
        missing.AddRange(BotNeeds.Where(x => !botPermissions.Has(x.Permission))
            .Select(x => x.Name));

        var userCan = member?.GetPermissions(channel).UseApplicationCommands;

        var everyone = guild.EveryoneRole;
        var overwrite = channel.GetPermissionOverwrite(everyone);
        var everyoneCan = overwrite?.UseApplicationCommands switch
        {
            PermValue.Allow => true,
            PermValue.Deny => false,
            _ => everyone.Permissions.UseApplicationCommands
        };

        return (missing, userCan, everyoneCan);
    }
}
