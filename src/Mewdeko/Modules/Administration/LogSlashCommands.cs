using System.Text;
using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Services;
using LogType = Mewdeko.Modules.Administration.Services.LogCommandService.LogType;

namespace Mewdeko.Modules.Administration;

/// <summary>
///     Implementation of logging commands as slash commands
/// </summary>
[Group("log", "Configure logging settings for your server")]
public class LogSlashCommands : MewdekoSlashModuleBase<LogCommandService>
{
    /// <summary>
    ///     LogMessages
    /// </summary>
    public static readonly ConcurrentDictionary<ulong, IUserMessage> LogSelectMessages = new();

    private readonly GuildSettingsService gss;
    private readonly ILogger<LogSlashCommands> logger;


    /// <summary>
    ///     Initializes a new instance of the LogSlashCommands class.
    /// </summary>
    /// <param name="gss">Service for managing guild settings</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public LogSlashCommands(GuildSettingsService gss, ILogger<LogSlashCommands> logger)
    {
        this.gss = gss;
        this.logger = logger;
    }

    /// <summary>
    ///     Base log command, shows select menu when only channel is provided
    /// </summary>
    [SlashCommand("single", "Configure logging channels")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task LogSet(
        [Summary("channel", "The channel to send logs to")]
        ITextChannel channel,
        [Summary("type", "The type of event to log")]
        LogType? type = null)
    {
        if (type == null)
        {
            await ShowLogSelectMenu(channel);
            return;
        }

        try
        {
            await Service.SetLogChannel(Context.Guild.Id, channel.Id, type.Value);
            await ReplyConfirmAsync(Strings.LoggingEventEnabled(Context.Guild.Id, type.Value, channel.Id));
        }
        catch (Exception e)
        {
            logger.LogError(e, "There was an issue setting logs");
            await ReplyConfirmAsync(Strings.CommandFatalError(Context.Guild.Id));
        }
    }

    /// <summary>
    ///     Sets the logging category for a specified type of logs.
    /// </summary>
    [SlashCommand("category", "Set logging category for specific types")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Category(
        LogCommandService.LogCategoryTypes type,
        ITextChannel? channel = null)
    {
        await Service.LogSetByType(Context.Guild.Id, channel?.Id ?? 0, type);
        if (channel is null)
        {
            await ReplyConfirmAsync(Strings.LoggingCategoryDisabled(Context.Guild.Id, type));
            return;
        }

        await ReplyConfirmAsync(Strings.LoggingCategoryEnabled(Context.Guild.Id, type, channel.Mention));
    }

    /// <summary>
    ///     Sets multiple logging channels for specified event types at once.
    /// </summary>
    [SlashCommand("multiple", "Set multiple log types for a channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Multiple(
        [Summary("channel", "The channel to log to")]
        ITextChannel channel,
        [Summary("types", "Comma-separated list of log types")]
        string types)
    {
        var typesList = types.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => Enum.TryParse<LogType>(t, true, out _))
            .Select(t => Enum.Parse<LogType>(t, true))
            .ToArray();

        if (!typesList.Any())
        {
            await ReplyConfirmAsync("You must specify at least one valid log type.");
            return;
        }

        await SetMultipleLogTypes(new Dictionary<ITextChannel, LogType[]>
        {
            {
                channel, typesList
            }
        });
    }

    /// <summary>
    ///     Sets multiple logging channels for different event types using channel-type pairs.
    /// </summary>
    [SlashCommand("pairs", "Configure multiple channel-type pairs")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Pairs(
        [Summary("pairs", "Channel-type pairs (format: channelId:Type1,Type2;channelId:Type3,Type4)")]
        string pairs)
    {
        var channelPairs = pairs.Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (!channelPairs.Any())
        {
            await ReplyConfirmAsync("You must specify at least one channel:type pair.");
            return;
        }

        var channelTypeMap = new Dictionary<ITextChannel, LogType[]>();
        var errors = new List<string>();

        foreach (var pair in channelPairs)
        {
            var splitPair = pair.Split(':', 2);
            if (splitPair.Length != 2)
            {
                errors.Add($"Invalid format for pair: {pair}. Expected format: channelId:Type1,Type2");
                continue;
            }

            if (!ulong.TryParse(splitPair[0].Trim(), out var channelId))
            {
                errors.Add($"Invalid channel ID: {splitPair[0]}");
                continue;
            }

            var channel = await Context.Guild.GetTextChannelAsync(channelId);
            if (channel == null)
            {
                errors.Add($"Could not find channel with ID: {channelId}");
                continue;
            }

            var typesList = splitPair[1].Split(',')
                .Select(t => t.Trim())
                .Where(t => Enum.TryParse<LogType>(t, true, out _))
                .Select(t => Enum.Parse<LogType>(t, true))
                .ToArray();

            if (typesList.Any())
            {
                channelTypeMap[channel] = typesList;
            }
        }

        if (errors.Any())
        {
            await ReplyConfirmAsync($"Encountered the following errors:\n{string.Join("\n", errors)}");
            return;
        }

        await SetMultipleLogTypes(channelTypeMap);
    }

    /// <summary>
    ///     Displays the current logging events configuration for the guild.
    /// </summary>
    [SlashCommand("events", "Display current logging configuration")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Events()
    {
        Service.GuildLogSettings.TryGetValue(Context.Guild.Id, out var l);
        var str = string.Join("\n", Enum.GetNames(typeof(LogType)).OrderBy(x => x).Select(x =>
        {
            var val = l == null ? null : GetLogProperty(l, Enum.Parse<LogType>(x));
            return val != null && val != 0 ? $"{Format.Bold(x)} <#{val}>" : Format.Bold(x);
        }));

        await ReplyConfirmAsync($"{Format.Bold(Strings.LogEvents(Context.Guild.Id))}\n{str}");
    }

    /// <summary>
    ///     Sets a channel to log commands to
    /// </summary>
    [SlashCommand("commandchannel", "Set channel for command logging")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task CommandChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await ReplyConfirmAsync(Strings.CommandLoggingDisabled(Context.Guild.Id));
            var gc = await gss.GetGuildConfig(Context.Guild.Id);
            gc.CommandLogChannel = 0;
            await gss.UpdateGuildConfig(Context.Guild.Id, gc);
        }
        else
        {
            await ReplyConfirmAsync(Strings.CommandLoggingEnabled(Context.Guild.Id));
            var gc = await gss.GetGuildConfig(Context.Guild.Id);
            gc.CommandLogChannel = channel.Id;
            await gss.UpdateGuildConfig(Context.Guild.Id, gc);
        }
    }

    /// <summary>
    ///     Toggles a channel as ignored for logging events. When a channel is ignored, no log events that occur in that
    ///     channel will be logged.
    /// </summary>
    /// <param name="channel">The channel to toggle ignore status for. If not specified, uses the current channel.</param>
    [SlashCommand("ignore", "Toggles whether a channel is ignored for logging")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task LogIgnore(
        [Summary("channel", "The channel to toggle, defaults to the current channel")]
        ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var result = await Service.LogIgnore(ctx.Guild.Id, channel.Id);

        switch (result)
        {
            case LogCommandService.IgnoreResult.Added:
                await ConfirmAsync(Strings.LogIgnoreChannelAdded(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
                break;
            case LogCommandService.IgnoreResult.Removed:
                await ConfirmAsync(Strings.LogIgnoreChannelRemoved(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
                break;
            default:
                await ErrorAsync(Strings.LogIgnoreError(ctx.Guild.Id)).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Lists all channels that are currently ignored for logging.
    /// </summary>
    [SlashCommand("ignore-list", "Lists every channel ignored for logging")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task LogIgnoreList()
    {
        await DeferAsync();
        var ignoredChannels = await Service.GetIgnoredChannels(ctx.Guild.Id);

        if (ignoredChannels.Count == 0)
        {
            await ConfirmAsync(Strings.LogIgnoreListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var channelMentions = new List<string>();
        foreach (var channelId in ignoredChannels)
        {
            var ch = await ctx.Guild.GetChannelAsync(channelId);
            if (ch is ITextChannel textChannel)
                channelMentions.Add(textChannel.Mention);
        }

        if (channelMentions.Count == 0)
        {
            await ConfirmAsync(Strings.LogIgnoreListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor()
            .WithTitle(Strings.LogIgnoreList(ctx.Guild.Id))
            .WithDescription(string.Join("\n", channelMentions)).Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the interaction when users select log types from the select menu.
    /// </summary>
    /// <param name="channelId">The ID of the channel to set up logging for</param>
    /// <param name="menuIndex">The index of the select menu that was interacted with</param>
    /// <param name="selected">Array of selected log type names</param>
    [ComponentInteraction("logselect:*:*", true)]
    public async Task HandleLogSelect(string channelId, string menuIndex, string[] selected)
    {
        var inter = ctx.Interaction as IComponentInteraction;
        if (!ulong.TryParse(channelId, out var chanId))
        {
            await RespondAsync("Invalid channel ID", ephemeral: true);
            return;
        }

        var channel = await Context.Guild.GetTextChannelAsync(chanId);
        if (channel == null)
        {
            await RespondAsync("Could not find the specified channel", ephemeral: true);
            return;
        }

        try
        {
            // Get current settings
            Service.GuildLogSettings.TryGetValue(Context.Guild.Id, out var currentSettings);
            var previouslySelected = new HashSet<LogType>();

            if (currentSettings != null)
            {
                // Build list of previously enabled log types for this channel
                foreach (var logType in Enum.GetValues<LogType>())
                {
                    var currentChannelId = GetLogProperty(currentSettings, logType);
                    if (currentChannelId == channel.Id)
                    {
                        previouslySelected.Add(logType);
                    }
                }
            }

            // Get newly selected types
            var newlySelected = selected.Select(Enum.Parse<LogType>).ToHashSet();

            // Types to disable (were selected before but not in new selection)
            var typesToDisable = previouslySelected.Where(t => !newlySelected.Contains(t));

            // Types to enable (new selections)
            var typesToEnable = newlySelected.Where(t => !previouslySelected.Contains(t));

            // Disable logging for deselected types
            foreach (var type in typesToDisable)
            {
                await Service.SetLogChannel(Context.Guild.Id, 0, type);
            }

            // Enable logging for newly selected types
            foreach (var type in typesToEnable)
            {
                await Service.SetLogChannel(Context.Guild.Id, channel.Id, type);
            }

            // Build response message
            var response = new StringBuilder();
            if (typesToEnable.Any())
            {
                response.AppendLine($"Enabled logging for: {string.Join(", ", typesToEnable.Select(t => $"**{t}**"))}");
            }

            if (typesToDisable.Any())
            {
                response.AppendLine(
                    $"Disabled logging for: {string.Join(", ", typesToDisable.Select(t => $"**{t}**"))}");
            }

            // Clean up the select menu message
            if (LogSelectMessages.TryGetValue(Context.Channel.Id, out _))
            {
                try
                {
                    LogSelectMessages.TryRemove(Context.Channel.Id, out _);
                }
                catch
                {
                    // ignored
                }
            }

            await inter.UpdateAsync(x =>
            {
                x.Embed = new EmbedBuilder().WithOkColor().WithDescription(response.ToString()).Build();
                x.Components = null;
            });
        }
        catch (Exception e)
        {
            await RespondAsync(Strings.CommandFatalError(Context.Guild.Id), ephemeral: true);
            logger.LogError(e, "There was an issue setting logs");
        }
    }

    private async Task ShowLogSelectMenu(ITextChannel channel)
    {
        await DeferAsync();
        var logTypes = Enum.GetValues<LogType>().ToList();
        var componentBuilder = new ComponentBuilder();

        // Get current log settings
        Service.GuildLogSettings.TryGetValue(Context.Guild.Id, out var currentSettings);
        var selectedTypes = new HashSet<LogType>();

        if (currentSettings != null)
        {
            // Build list of currently enabled log types
            foreach (var logType in from logType in logTypes
                     let channelId = GetLogProperty(currentSettings, logType)
                     where channelId == channel.Id
                     select logType)
            {
                selectedTypes.Add(logType);
            }
        }

        // Split into chunks of 25
        for (var i = 0; i < logTypes.Count; i += 25)
        {
            var menuIndex = i / 25;
            var menuBuilder = new SelectMenuBuilder()
                .WithCustomId($"logselect:{channel.Id}:{menuIndex}")
                .WithMinValues(1)
                .WithMaxValues(Math.Min(25, logTypes.Count - i))
                .WithPlaceholder($"Select log types to enable ({menuIndex + 1})...");

            // Add options for this menu (up to 25)
            foreach (var logType in logTypes.Skip(i).Take(25))
            {
                menuBuilder.AddOption(
                    logType.ToString(),
                    logType.ToString(),
                    $"Enable logging for {logType} events",
                    isDefault: selectedTypes.Contains(logType) // Preselect if currently enabled
                );
            }

            componentBuilder.WithSelectMenu(menuBuilder);
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithDescription($"{Strings.LogEventsSelect(ctx.Guild.Id, channel.Mention)}\n" +
                             (selectedTypes.Any()
                                 ? $"\n\nCurrently enabled types: {string.Join(", ", selectedTypes)}"
                                 : ""))
            .Build();

        var msg = await ctx.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Embed = eb;
                x.Components = componentBuilder.Build();
            }
        );

        if (LogSelectMessages.TryGetValue(Context.Channel.Id, out _))
        {
            try
            {
                LogSelectMessages.TryRemove(Context.Channel.Id, out _);
            }
            catch
            {
                // ignored
            }
        }

        LogSelectMessages[Context.Channel.Id] = msg;
    }

    private async Task SetMultipleLogTypes(Dictionary<ITextChannel, LogType[]> channelTypeMap)
    {
        try
        {
            var confirmations = new List<string>();

            foreach (var (channel, types) in channelTypeMap)
            {
                foreach (var type in types)
                {
                    await Service.SetLogChannel(Context.Guild.Id, channel.Id, type);
                }

                var typesList = string.Join(", ", types.Select(t => $"**{t}**"));
                confirmations.Add($"Enabled {typesList} in {channel.Mention}");
            }

            await ReplyConfirmAsync($"Logging has been configured:\n{string.Join("\n", confirmations)}");
        }
        catch (Exception e)
        {
            logger.LogError(e, "There was an issue setting logs");
            await ReplyConfirmAsync(Strings.CommandFatalError(Context.Guild.Id));
        }
    }

    private static ulong? GetLogProperty(LoggingV2 l, LogType type)
    {
        return type switch
        {
            LogType.Other => l.LogOtherId,
            LogType.MessageUpdated => l.MessageUpdatedId,
            LogType.UserUpdated => l.UserUpdatedId,
            LogType.MessageDeleted => l.MessageDeletedId,
            LogType.UserJoined => l.UserJoinedId,
            LogType.UserLeft => l.UserLeftId,
            LogType.UserBanned => l.UserBannedId,
            LogType.UserUnbanned => l.UserUnbannedId,
            LogType.ChannelCreated => l.ChannelCreatedId,
            LogType.ChannelDestroyed => l.ChannelDestroyedId,
            LogType.ChannelUpdated => l.ChannelUpdatedId,
            LogType.VoicePresence => l.LogVoicePresenceId,
            LogType.VoicePresenceTts => l.LogVoicePresenceTtsId,
            LogType.UserMuted => l.UserMutedId,
            LogType.EventCreated => l.EventCreatedId,
            LogType.ThreadCreated => l.ThreadCreatedId,
            LogType.ThreadDeleted => l.ThreadDeletedId,
            LogType.ThreadUpdated => l.ThreadUpdatedId,
            LogType.NicknameUpdated => l.NicknameUpdatedId,
            LogType.RoleCreated => l.RoleCreatedId,
            LogType.RoleDeleted => l.RoleDeletedId,
            LogType.RoleUpdated => l.RoleUpdatedId,
            LogType.ServerUpdated => l.ServerUpdatedId,
            LogType.UserRoleAdded => l.UserRoleAddedId,
            LogType.UserRoleRemoved => l.UserRoleRemovedId,
            LogType.UsernameUpdated => l.UsernameUpdatedId,
            LogType.AvatarUpdated => l.AvatarUpdatedId,
            LogType.InviteCreated => l.InviteCreatedId,
            LogType.InviteDeleted => l.InviteDeletedId,
            LogType.MessagesBulkDeleted => l.MessagesBulkDeletedId,
            LogType.ReactionEvents => l.ReactionEventsId,
            _ => null
        };
    }
}