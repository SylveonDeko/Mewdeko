using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;
using LogType = Mewdeko.Modules.Administration.Services.LogCommandService.LogType;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Module for logging commands.
    /// </summary>
    [Group]
    public class LogCommands(GuildSettingsService gss, ILogger<LogCommands> logger)
        : MewdekoSubmodule<LogCommandService>
    {
        /// <summary>
        ///     Sets the logging category for a specified type of logs.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <param name="type">The type of logs to set the category for.</param>
        /// <param name="channel">The text channel where the logs will be sent.</param>
        /// <example>.logcategory messages #log-channel</example>
        /// <example>.logcategory messages</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(1)]
        public async Task LogCategory(LogCommandService.LogCategoryTypes type, ITextChannel? channel = null)
        {
            await Service.LogSetByType(ctx.Guild.Id, channel?.Id ?? 0, type);
            if (channel is null)
            {
                await ctx.Channel.SendConfirmAsync(Strings.LoggingCategoryDisabled(ctx.Guild.Id, type));
                return;
            }

            await ctx.Channel.SendConfirmAsync(Strings.LoggingCategoryEnabled(ctx.Guild.Id, type, channel.Mention));
        }

        /// <summary>
        ///     Sets multiple logging channels for specified event types at once.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <param name="channel">The channel to set as the logging channel for all specified types.</param>
        /// <example>.log #log-channel UserJoined UserLeft UserBanned</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(4)]
        public async Task Log(ITextChannel channel) => await ShowLogSelectMenu(channel);

        /// <summary>
        ///     Sets multiple logging channels for specified event types at once.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <param name="channel">The channel to set as the logging channel for all specified types.</param>
        /// <param name="types">The types of events to set the logging channel for.</param>
        /// <example>.log #log-channel UserJoined UserLeft UserBanned</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(1)]
        public async Task Log(ITextChannel channel, params LogType[]? types)
        {
            if (types is null || types.Length == 0)
            {
                await ShowLogSelectMenu(channel);
                return;
            }

            await SetMultipleLogTypes(new Dictionary<ITextChannel, LogType[]>
            {
                {
                    channel, types
                }
            });
        }

        /// <summary>
        ///     Sets multiple logging channels for different event types at once using channel-type pairs.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        ///     Format: .log "channel1:type1,type2" "channel2:type3,type4"
        /// </remarks>
        /// <param name="channelTypePairs">Strings containing channel-type pairs in the format "#channel:Type1,Type2"</param>
        /// <example>.log "#server-logs:ServerUpdated,UserBanned" "#thread-logs:ThreadCreated,ThreadUpdated,ThreadDeleted"</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(2)]
        public async Task Log(params string[] channelTypePairs)
        {
            if (channelTypePairs.Length == 0)
            {
                await ErrorAsync("You must specify at least one channel:type pair.");
                return;
            }

            var channelTypeMap = new Dictionary<ITextChannel, LogType[]>();
            var errors = new List<string>();

            foreach (var pair in channelTypePairs)
            {
                var splitPair = pair.Split(':', 2);
                if (splitPair.Length != 2)
                {
                    errors.Add($"Invalid format for pair: {pair}. Expected format: #channel:Type1,Type2");
                    continue;
                }

                var channelMention = splitPair[0].Trim();
                var typesList = splitPair[1].Split(',').Select(t => t.Trim());

                // Extract channel ID from mention format (<#123456789>)
                var channelIdStr = channelMention.Trim('<', '>', '#');
                if (!ulong.TryParse(channelIdStr, out var channelId))
                {
                    errors.Add($"Invalid channel format: {channelMention}");
                    continue;
                }

                var channel = await ctx.Guild.GetTextChannelAsync(channelId);
                if (channel == null)
                {
                    errors.Add($"Could not find channel: {channelMention}");
                    continue;
                }

                var logTypes = new List<LogType>();
                foreach (var typeStr in typesList)
                {
                    if (Enum.TryParse<LogType>(typeStr, true, out var logType))
                    {
                        logTypes.Add(logType);
                    }
                    else
                    {
                        errors.Add($"Invalid log type: {typeStr}");
                    }
                }

                if (logTypes.Any())
                {
                    channelTypeMap[channel] = logTypes.ToArray();
                }
            }

            if (errors.Any())
            {
                await ErrorAsync($"Encountered the following errors:\n{string.Join("\n", errors)}");
                return;
            }

            await SetMultipleLogTypes(channelTypeMap);
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
                        await Service.SetLogChannel(ctx.Guild.Id, channel.Id, type);
                    }

                    var typesList = string.Join(", ", types.Select(t => $"**{t}**"));
                    confirmations.Add($"Enabled {typesList} in {channel.Mention}");
                }

                await ConfirmAsync($"Logging has been configured:\n{string.Join("\n", confirmations)}");
            }
            catch (Exception e)
            {
                logger.LogError(e, "There was an issue setting logs");
                await ErrorAsync(Strings.CommandFatalError(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Displays the current logging events configuration for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <example>.logevents</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task LogEvents()
        {
            Service.GuildLogSettings.TryGetValue(ctx.Guild.Id, out var l);
            var str = string.Join("\n", Enum.GetNames(typeof(LogType)).OrderBy(x => x).Select(x =>
            {
                var val = l == null ? null : GetLogProperty(l, Enum.Parse<LogType>(x));
                return val != null && val != 0 ? $"{Format.Bold(x)} <#{val}>" : Format.Bold(x);
            }));

            await ctx.Channel.SendConfirmAsync($"{Format.Bold(Strings.LogEvents(ctx.Guild.Id))}\n{str}")
                .ConfigureAwait(false);
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


        /// <summary>
        ///     Sets the logging channel for a specific event type.
        /// </summary>
        /// <param name="type">The type of event to set the logging channel for.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <param name="channel">
        ///     The channel to set as the logging channel. If not provided, the command will disable logging for
        ///     the specified event type.
        /// </param>
        /// <example>.log UserJoined #log-channel</example>
        /// <example>.log UserLeft</example>
        /// <example>.log UserJoined</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task Log(LogType type, ITextChannel? channel = null)
        {
            try
            {
                channel ??= ctx.Channel as ITextChannel;
                await Service.SetLogChannel(ctx.Guild.Id, channel?.Id ?? 0, type).ConfigureAwait(false);
                if (channel is not null)
                {
                    await ConfirmAsync(Strings.LoggingEventEnabled(ctx.Guild.Id, type, channel.Id))
                        .ConfigureAwait(false);
                    return;
                }

                await ConfirmAsync(Strings.LoggingEventDisabled(ctx.Guild.Id, type));
            }
            catch (Exception e)
            {
                logger.LogError(e, "There was an issue setting logs");
                await ErrorAsync(Strings.CommandFatalError(ctx.Guild.Id));
            }
        }


        /// <summary>
        ///     Sets a channel to log commands to
        /// </summary>
        /// <param name="channel">The channel to log commands to</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CommandLogChannel(ITextChannel? channel = null)
        {
            if (channel is null)
            {
                await ConfirmAsync(Strings.CommandLoggingDisabled(ctx.Guild.Id));
                var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                gc.CommandLogChannel = 0;
                await gss.UpdateGuildConfig(ctx.Guild.Id, gc);
            }
            else
            {
                await ConfirmAsync(Strings.CommandLoggingEnabled(ctx.Guild.Id));
                var gc = await gss.GetGuildConfig(ctx.Guild.Id);
                gc.CommandLogChannel = channel.Id;
                await gss.UpdateGuildConfig(ctx.Guild.Id, gc);
            }
        }

        private async Task ShowLogSelectMenu(ITextChannel channel)
        {
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

            var msg = await ctx.Channel.SendMessageAsync(
                embed: eb,
                components: componentBuilder.Build()
            );

            if (!LogSlashCommands.LogSelectMessages.TryGetValue(Context.Channel.Id, out var oldMsg))
            {
                try
                {
                    await oldMsg.DeleteAsync();
                    LogSlashCommands.LogSelectMessages.TryRemove(Context.Channel.Id, out _);
                }
                catch
                {
                    // ignored
                }
            }

            LogSlashCommands.LogSelectMessages[Context.Channel.Id] = msg;
        }

        /// <summary>
        ///     Toggles a channel as ignored for logging events.
        /// </summary>
        /// <remarks>
        ///     When a channel is ignored, no log events that occur in that channel will be logged.
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <param name="channel">The channel to toggle ignore status for. If not specified, uses the current channel.</param>
        /// <example>.logignore #general</example>
        /// <example>.logignore</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task LogIgnore(ITextChannel? channel = null)
        {
            channel ??= (ITextChannel)ctx.Channel;

            var result = await Service.LogIgnore(ctx.Guild.Id, channel.Id);

            switch (result)
            {
                case LogCommandService.IgnoreResult.Added:
                    await ctx.Channel.SendConfirmAsync(
                            Strings.LogIgnoreChannelAdded(ctx.Guild.Id, channel.Mention))
                        .ConfigureAwait(false);
                    break;
                case LogCommandService.IgnoreResult.Removed:
                    await ctx.Channel.SendConfirmAsync(
                            Strings.LogIgnoreChannelRemoved(ctx.Guild.Id, channel.Mention))
                        .ConfigureAwait(false);
                    break;
                default:
                    await ErrorAsync(
                            Strings.LogIgnoreError(ctx.Guild.Id))
                        .ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        ///     Lists all channels that are currently ignored for logging.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <example>.logignorelist</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task LogIgnoreList()
        {
            var ignoredChannels = await Service.GetIgnoredChannels(ctx.Guild.Id);

            if (!ignoredChannels.Any())
            {
                await ctx.Channel.SendConfirmAsync(Strings.LogIgnoreListEmpty(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            var channelMentions = ignoredChannels
                .Select(channelId => ctx.Guild.GetChannelAsync(channelId).GetAwaiter().GetResult())
                .Where(ch => ch != null)
                .OfType<ITextChannel>()
                .Select(ch => ch.Mention)
                .ToList();

            if (!channelMentions.Any())
            {
                await ctx.Channel.SendConfirmAsync(Strings.LogIgnoreListEmpty(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            await ctx.Channel.SendConfirmAsync(
                    Strings.LogIgnoreList(ctx.Guild.Id),
                    string.Join("\n", channelMentions))
                .ConfigureAwait(false);
        }
    }
}