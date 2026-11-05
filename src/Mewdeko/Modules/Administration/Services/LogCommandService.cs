using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using Discord.Net;
using Discord.Rest;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Strings;
using DiscordShardedClient = Discord.WebSocket.DiscordShardedClient;

// ReSharper disable GrammarMistakeInComment

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service for managing log commands.
/// </summary>
/// <param name="dbFactory">The database service.</param>
/// <param name="client">The Discord client.</param>
/// <param name="handler">The event handler.</param>
/// <param name="muteService">The mute service.</param>
/// <param name="strings">The localization service.</param>
/// <param name="logger">The logger instance for structured logging.</param>
/// <param name="collector">The analytics collector.</param>
public class LogCommandService(
    IDataConnectionFactory dbFactory,
    DiscordShardedClient client,
    EventHandler handler,
    MuteService muteService,
    GeneratedBotStrings strings,
    ILogger<LogCommandService> logger,
    IAnalyticsCollector collector) : INService, IReadyExecutor
{
    /// <summary>
    ///     Result of toggling a channel's ignore status
    /// </summary>
    public enum IgnoreResult
    {
        /// <summary>Channel was added to ignore list</summary>
        Added,

        /// <summary>Channel was removed from ignore list</summary>
        Removed,

        /// <summary>An error occurred</summary>
        Error
    }

    /// <summary>
    ///     Log category types.
    /// </summary>
    public enum LogCategoryTypes
    {
        /// <summary> All available log event types. </summary>
        All,

        /// <summary> Events related to users (join, leave, roles, name changes, voice). </summary>
        Users,

        /// <summary> Events related to thread creation, deletion, updates. </summary>
        Threads,

        /// <summary> Events related to role creation, deletion, updates. </summary>
        Roles,

        /// <summary> Events related to server-wide settings changes and server events. </summary>
        Server,

        /// <summary> Events related to channel creation, deletion, updates. </summary>
        Channel,

        /// <summary> Events related to message updates and deletions. </summary>
        Messages,

        /// <summary> Events related to moderation actions (bans, unbans, mutes). </summary>
        Moderation,

        /// <summary> Disables logging for all event types. </summary>
        None
    }

    /// <summary>
    ///     Dictionary of log types.
    /// </summary>
    public enum LogType
    {
        /// <summary> Miscellaneous or custom log events not covered by other types. </summary>
        Other,

        /// <summary> A scheduled server event was created. </summary>
        EventCreated,

        /// <summary> A role's properties (name, color, perms, etc.) were updated. </summary>
        RoleUpdated,

        /// <summary> A new role was created. </summary>
        RoleCreated,

        /// <summary> A role was deleted. </summary>
        RoleDeleted,

        /// <summary> Server settings (name, icon, owner, etc.) were updated. </summary>
        ServerUpdated,

        /// <summary> A new thread was created. </summary>
        ThreadCreated,

        /// <summary> One or more roles were added to a user. </summary>
        UserRoleAdded,

        /// <summary> One or more roles were removed from a user. </summary>
        UserRoleRemoved,

        /// <summary> A user's global username changed. </summary>
        UsernameUpdated,

        /// <summary> A user's server nickname changed. </summary>
        NicknameUpdated,

        /// <summary> A user's avatar changed. </summary>
        AvatarUpdated,

        /// <summary> A thread was deleted. </summary>
        ThreadDeleted,

        /// <summary> A thread's properties (name, archive status, etc.) were updated. </summary>
        ThreadUpdated,

        /// <summary> A message's content was edited. </summary>
        MessageUpdated,

        /// <summary> A message was deleted. </summary>
        MessageDeleted,

        /// <summary> A user joined the server. </summary>
        UserJoined,

        /// <summary> A user left (or was kicked from) the server. </summary>
        UserLeft,

        /// <summary> A user was banned from the server. </summary>
        UserBanned,

        /// <summary> A user was unbanned from the server. </summary>
        UserUnbanned,

        /// <summary> A user's profile properties (avatar, global name) changed. </summary>
        UserUpdated,

        /// <summary> A new channel was created. </summary>
        ChannelCreated,

        /// <summary> A channel was deleted. </summary>
        ChannelDestroyed,

        /// <summary> A channel's properties (name, topic, permissions, etc.) were updated. </summary>
        ChannelUpdated,

        /// <summary> A user's voice state changed (join, leave, move, mute, deafen, stream). </summary>
        VoicePresence,

        /// <summary> A user potentially used Text-to-Speech in a voice channel (Detection not guaranteed). </summary>
        VoicePresenceTts,

        /// <summary> A user was muted (voice or text). </summary>
        UserMuted,

        /// <summary> An invite was created. </summary>
        InviteCreated,

        /// <summary> An invite was deleted. </summary>
        InviteDeleted,

        /// <summary> Multiple messages were bulk deleted. </summary>
        MessagesBulkDeleted,

        /// <summary> Reactions were added or removed from messages. </summary>
        ReactionEvents,

        /// <summary> A custom emoji was created. </summary>
        EmojiCreated,

        /// <summary> A custom emoji was renamed or had its roles changed. </summary>
        EmojiUpdated,

        /// <summary> A custom emoji was deleted. </summary>
        EmojiDeleted,

        /// <summary> A custom sticker was created. </summary>
        StickerCreated,

        /// <summary> A custom sticker was edited. </summary>
        StickerUpdated,

        /// <summary> A custom sticker was deleted. </summary>
        StickerDeleted,

        /// <summary> A member was timed out. </summary>
        UserTimedOut,

        /// <summary> A member's timeout was removed early. </summary>
        UserTimeoutRemoved,

        /// <summary> A message containing a Discord invite link was posted. </summary>
        InviteLinkPosted,

        /// <summary> A channel permission overwrite was added, changed or removed. </summary>
        ChannelPermissionsUpdated,

        /// <summary> A member boosted the server. </summary>
        ServerBoosted,

        /// <summary> An integration was added, changed or removed. </summary>
        IntegrationChanged,

        /// <summary> A webhook was created, changed or deleted. </summary>
        WebhookChanged,

        /// <summary> A stage was started, changed or ended. </summary>
        StageChanged,

        /// <summary> A scheduled event was changed or deleted. </summary>
        ScheduledEventChanged,

        /// <summary> An AutoMod rule was created, changed or deleted. </summary>
        AutoModRuleChanged,

        /// <summary> A soundboard sound was added, changed or removed. </summary>
        SoundboardChanged,

        /// <summary> A member joined or left a thread. </summary>
        ThreadMemberChanged
    }

    /// <summary>
    ///     Result of importing a log configuration.
    /// </summary>
    /// <param name="Applied">The log types that were mapped to a channel.</param>
    /// <param name="Unmapped">Descriptions of channels that could not be mapped.</param>
    /// <param name="Valid">Whether the code or file could be read.</param>
    public record LogImportResult(int Applied, List<string> Unmapped, bool Valid);

    private sealed class LogConfigExport
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, ExportedChannel> Types { get; set; } = new();
        public List<ExportedChannel> Ignored { get; set; } = [];
        public List<string> Prefixes { get; set; } = [];
    }

    private sealed class ExportedChannel
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = "";
    }

    private static readonly Regex InviteRegex = new(
        @"(?:discord\.gg|discord(?:app)?\.com/invite)/[A-Za-z0-9-]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    ///     Cache of ignored channels per guild
    /// </summary>
    private readonly ConcurrentDictionary<ulong, HashSet<ulong>> ignoredChannelsCache = new();

    /// <summary>
    ///     Reaction events batching system to prevent spam in high-activity servers
    /// </summary>
    private readonly ConcurrentDictionary<ulong, MessageReactionBatch> reactionBatches = new();

    private Timer reactionBatchTimer;

    private Timer timeoutEndTimer;

    private readonly SemaphoreSlim timeoutCheckLock = new(1, 1);

    /// <summary>
    ///     Dictionary of log settings for each guild.
    /// </summary>
    public ConcurrentDictionary<ulong, LoggingV2> GuildLogSettings { get; set; }


    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        try
        {
            var logSettingsList = await db.LoggingV2.ToListAsync().ConfigureAwait(false);
            var dict = logSettingsList.ToDictionary(g => g.GuildId, g => g);
            GuildLogSettings = new ConcurrentDictionary<ulong, LoggingV2>(dict);

            // Load ignored channels
            var allIgnoredChannels = await db.GetTable<LogIgnoredChannel>()
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var group in allIgnoredChannels.GroupBy(ic => ic.GuildId))
            {
                ignoredChannelsCache[group.Key] = group.Select(ic => ic.ChannelId).ToHashSet();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load LoggingV2 settings on ready.");
            GuildLogSettings = new ConcurrentDictionary<ulong, LoggingV2>();
        }

        // yeaaaa would be a good idea to add events AFTER we get all guild settings.
        handler.Subscribe("GuildScheduledEventCreated", "LogCommandService", OnEventCreated);
        handler.Subscribe("RoleUpdated", "LogCommandService", OnRoleUpdated);
        handler.Subscribe("RoleCreated", "LogCommandService", OnRoleCreated);
        handler.Subscribe("RoleDeleted", "LogCommandService", OnRoleDeleted);
        handler.Subscribe("GuildUpdated", "LogCommandService", OnGuildUpdated);
        handler.Subscribe("ThreadCreated", "LogCommandService", OnThreadCreated);
        handler.Subscribe("GuildMemberUpdated", "LogCommandService", OnUserRoleAdded);
        handler.Subscribe("GuildMemberUpdated", "LogCommandService", OnUserRoleRemoved);
        handler.Subscribe("UserUpdated", "LogCommandService", OnUsernameUpdated);
        handler.Subscribe("GuildMemberUpdated", "LogCommandService", OnNicknameUpdated);
        handler.Subscribe("ThreadDeleted", "LogCommandService", OnThreadDeleted);
        handler.Subscribe("ThreadUpdated", "LogCommandService", OnThreadUpdated);
        handler.Subscribe("MessageUpdated", "LogCommandService", OnMessageUpdated);
        handler.Subscribe("MessageDeleted", "LogCommandService", OnMessageDeleted);
        handler.Subscribe("UserJoined", "LogCommandService", OnUserJoined);
        handler.Subscribe("UserLeft", "LogCommandService", OnUserLeft);
        handler.Subscribe("ChannelCreated", "LogCommandService", OnChannelCreated);
        handler.Subscribe("ChannelDestroyed", "LogCommandService", OnChannelDestroyed);
        handler.Subscribe("ChannelUpdated", "LogCommandService", OnChannelUpdated);
        handler.Subscribe("UserVoiceStateUpdated", "LogCommandService", OnVoicePresence);
        handler.Subscribe("UserVoiceStateUpdated", "LogCommandService", OnVoicePresenceTts);
        handler.Subscribe("AuditLogCreated", "LogCommandService", OnAuditLogCreated);
        handler.Subscribe("InviteCreated", "LogCommandService", OnInviteCreated);
        handler.Subscribe("InviteDeleted", "LogCommandService", OnInviteDeleted);
        handler.Subscribe("MessagesBulkDeleted", "LogCommandService", OnMessagesBulkDeleted);
        handler.Subscribe("ReactionAdded", "LogCommandService", OnReactionAdded);
        handler.Subscribe("ReactionRemoved", "LogCommandService", OnReactionRemoved);
        handler.Subscribe("GuildUpdated", "LogCommandService", OnEmojisUpdated);
        handler.Subscribe("GuildStickerCreated", "LogCommandService", OnStickerCreated);
        handler.Subscribe("GuildStickerUpdated", "LogCommandService", OnStickerUpdated);
        handler.Subscribe("GuildStickerDeleted", "LogCommandService", OnStickerDeleted);
        handler.Subscribe("GuildMemberUpdated", "LogCommandService", OnUserTimeoutChanged);
        handler.Subscribe("MessageReceived", "LogCommandService", OnInviteLinkPosted);
        handler.Subscribe("MessageReceived", "LogCommandService", OnServerBoosted);
        handler.Subscribe("GuildScheduledEventStarted", "LogCommandService", OnEventStarted);
        handler.Subscribe("GuildScheduledEventCancelled", "LogCommandService", OnEventCancelled);
        handler.Subscribe("GuildScheduledEventCompleted", "LogCommandService", OnEventCompleted);
        handler.Subscribe("ThreadMemberJoined", "LogCommandService", OnThreadMemberJoined);
        handler.Subscribe("ThreadMemberLeft", "LogCommandService", OnThreadMemberLeft);
        muteService.UserMuted += OnUserMuted;
        muteService.UserUnmuted += OnUserUnmuted;

        // Initialize reaction batching timer
        reactionBatchTimer = new Timer(ProcessReactionBatches, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        timeoutEndTimer = new Timer(_ => _ = ProcessEndedTimeoutsAsync(), null, TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(15));
    }

    /// <summary>
    ///     Handles the creation of audit logs.
    /// </summary>
    /// <param name="args">The audit log entry.</param>
    /// <param name="arsg2">The guild where the audit log was created.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnAuditLogCreated(SocketAuditLogEntry args, SocketGuild arsg2)
    {
        switch (args.Action)
        {
            case ActionType.Ban:
            {
                if (args.Data is BanAuditLogData data)
                    await OnUserBanned(data.Target, arsg2, args.User, args.Reason);
                break;
            }
            case ActionType.Unban:
            {
                if (args.Data is UnbanAuditLogData data)
                    await OnUserUnbanned(data.Target, arsg2, args.User, args.Reason);
                break;
            }
            case ActionType.OverwriteCreated:
            case ActionType.OverwriteUpdated:
            case ActionType.OverwriteDeleted:
                await LogAuditChangeAsync(arsg2, args, l => l.ChannelPermissionsUpdatedId,
                    strings.LogChannelPermissionsChanged(arsg2.Id));
                break;
            case ActionType.IntegrationCreated:
            case ActionType.IntegrationUpdated:
            case ActionType.IntegrationDeleted:
                await LogAuditChangeAsync(arsg2, args, l => l.IntegrationChangedId,
                    strings.LogIntegrationChanged(arsg2.Id));
                break;
            case ActionType.WebhookCreated:
            case ActionType.WebhookUpdated:
            case ActionType.WebhookDeleted:
                await LogAuditChangeAsync(arsg2, args, l => l.WebhookChangedId,
                    strings.LogWebhookChanged(arsg2.Id));
                break;
            case ActionType.StageInstanceCreated:
            case ActionType.StageInstanceUpdated:
            case ActionType.StageInstanceDeleted:
                await LogAuditChangeAsync(arsg2, args, l => l.StageChangedId, strings.LogStageChanged(arsg2.Id));
                break;
            case ActionType.EventUpdate:
            case ActionType.EventDelete:
                await LogAuditChangeAsync(arsg2, args, l => l.ScheduledEventChangedId,
                    strings.LogScheduledEventChanged(arsg2.Id));
                break;
            case ActionType.AutoModerationRuleCreate:
            case ActionType.AutoModerationRuleUpdate:
            case ActionType.AutoModerationRuleDelete:
                await LogAuditChangeAsync(arsg2, args, l => l.AutoModRuleChangedId,
                    strings.LogAutoModRuleChanged(arsg2.Id));
                break;
            case ActionType.SoundboardSoundCreate:
            case ActionType.SoundboardSoundUpdate:
            case ActionType.SoundboardSoundDelete:
                await LogAuditChangeAsync(arsg2, args, l => l.SoundboardChangedId,
                    strings.LogSoundboardChanged(arsg2.Id));
                break;
        }
    }

    /// <summary>
    ///     Handles the creation of a role.
    /// </summary>
    /// <param name="args">The role that was created.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnRoleCreated(SocketRole args)
    {
        logger.LogDebug("LogCommandService.OnRoleCreated called for role {RoleName} in guild {GuildId}", args.Name,
            args.Guild.Id);
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            if (logSetting.RoleCreatedId is null or 0)
                return;

            var channel = args.Guild.GetTextChannel(logSetting.RoleCreatedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await GetAuditLogsSafeAsync(args.Guild, ActionType.RoleCreated);
            var auditLog = auditLogs.FirstOrDefault();
            if (auditLog == null) return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.RoleCreated(args.Guild.Id)}"),
                    new TextDisplayBuilder(
                        strings.NameField(args.Guild.Id, args.Name) +
                        strings.IdField(args.Guild.Id, args.Id) +
                        strings.ColorField(args.Guild.Id, args.Colors.PrimaryColor) +
                        strings.HoistedField(args.Guild.Id, args.IsHoisted) +
                        strings.MentionableField(args.Guild.Id, args.IsMentionable) +
                        strings.PositionField(args.Guild.Id, args.Position) +
                        strings.PermissionsField(args.Guild.Id, string.Join(", ", args.Permissions.ToList())) +
                        strings.CreatedByField(args.Guild.Id, auditLog.User.Mention, auditLog.User.Id) +
                        strings.ManagedField(args.Guild.Id, args.IsManaged))
                ], Mewdeko.OkColor);

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Fetches audit log entries without throwing when the bot lacks the View Audit Log permission. Guilds where the
    ///     permission is missing simply return no entries, so logging falls back to whatever it can report on its own.
    /// </summary>
    /// <param name="guild">The guild to fetch audit logs for.</param>
    /// <param name="actionType">The action type to filter by.</param>
    /// <param name="limit">The maximum number of entries to fetch.</param>
    /// <returns>The matching audit log entries, or an empty collection if they are unavailable.</returns>
    private async Task<IReadOnlyCollection<IAuditLogEntry>> GetAuditLogsSafeAsync(IGuild? guild, ActionType actionType,
        int limit = 1)
    {
        if (guild is null)
            return [];

        var botUser = client.GetGuild(guild.Id)?.CurrentUser;
        if (botUser is null || !botUser.GuildPermissions.ViewAuditLog)
            return [];

        try
        {
            return await guild.GetAuditLogsAsync(limit, actionType: actionType).ConfigureAwait(false);
        }
        catch (HttpException ex)
        {
            logger.LogDebug(ex, "Could not fetch {ActionType} audit logs for guild {GuildId}", actionType, guild.Id);
            return [];
        }
    }

    /// <summary>
    ///     Handles the event when a guild is updated.
    /// </summary>
    /// <param name="args">The original guild before the update.</param>
    /// <param name="arsg2">The updated guild.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnGuildUpdated(SocketGuild args, SocketGuild arsg2)
    {
        if (GuildLogSettings.TryGetValue(args.Id, out var logSetting))
        {
            if (logSetting.ServerUpdatedId is null or 0)
                return;

            var channel = args.GetTextChannel(logSetting.ServerUpdatedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await GetAuditLogsSafeAsync(args, ActionType.GuildUpdated);
            var auditLog = auditLogs.FirstOrDefault();

            var updatedByStr = auditLog is null
                ? "`Updated By:` Unknown"
                : $"`Updated By:` {auditLog.User.Mention} | {auditLog.User.Id}";
            var components = new ComponentBuilderV2();

            if (args.Name != arsg2.Name)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerNameUpdated(args.Id)}"),
                    new TextDisplayBuilder(
                        strings.ServerNameChangeDescription(args.Id, arsg2.Name, args.Name, updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.IconUrl != arsg2.IconUrl)
            {
                var mediaItems = new List<MediaGalleryItemProperties>();
                if (!string.IsNullOrEmpty(args.IconUrl))
                    mediaItems.Add(new MediaGalleryItemProperties
                    {
                        Media = new UnfurledMediaItemProperties(args.IconUrl), Description = "Before"
                    });
                if (!string.IsNullOrEmpty(arsg2.IconUrl))
                    mediaItems.Add(new MediaGalleryItemProperties
                    {
                        Media = new UnfurledMediaItemProperties(arsg2.IconUrl), Description = "After"
                    });

                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerIconUpdated(args.Id)}"),
                    new TextDisplayBuilder(updatedByStr)
                ], Mewdeko.OkColor);

                if (mediaItems.Count > 0)
                    components.WithMediaGallery(mediaItems);
            }
            else if (args.BannerUrl != arsg2.BannerUrl)
            {
                var mediaItems = new List<MediaGalleryItemProperties>();
                if (!string.IsNullOrEmpty(args.BannerUrl))
                    mediaItems.Add(new MediaGalleryItemProperties
                    {
                        Media = new UnfurledMediaItemProperties(args.BannerUrl), Description = "Before"
                    });
                if (!string.IsNullOrEmpty(arsg2.BannerUrl))
                    mediaItems.Add(new MediaGalleryItemProperties
                    {
                        Media = new UnfurledMediaItemProperties(arsg2.BannerUrl), Description = "After"
                    });

                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerBannerUpdated(args.Id)}"),
                    new TextDisplayBuilder(updatedByStr)
                ], Mewdeko.OkColor);

                if (mediaItems.Count > 0)
                    components.WithMediaGallery(mediaItems);
            }
            else if (args.SplashUrl != arsg2.SplashUrl)
            {
                var mediaItems = new List<MediaGalleryItemProperties>();
                if (!string.IsNullOrEmpty(args.SplashUrl))
                    mediaItems.Add(new MediaGalleryItemProperties
                    {
                        Media = new UnfurledMediaItemProperties(args.SplashUrl), Description = "Before"
                    });
                if (!string.IsNullOrEmpty(arsg2.SplashUrl))
                    mediaItems.Add(new MediaGalleryItemProperties
                    {
                        Media = new UnfurledMediaItemProperties(arsg2.SplashUrl), Description = "After"
                    });

                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerSplashUpdated(args.Id)}"),
                    new TextDisplayBuilder(updatedByStr)
                ], Mewdeko.OkColor);

                if (mediaItems.Count > 0)
                    components.WithMediaGallery(mediaItems);
            }
            else if (args.VanityURLCode != arsg2.VanityURLCode)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerVanityUrlUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerVanityUrlChange(args.Id,
                        arsg2.VanityURLCode ?? strings.None(args.Id),
                        args.VanityURLCode ?? strings.None(args.Id), updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.OwnerId != arsg2.OwnerId)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerOwnerUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerOwnerChange(args.Id, arsg2.Owner.Mention, arsg2.Owner.Id,
                        args.Owner.Mention,
                        args.Owner.Id, updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.AFKChannel?.Id != arsg2.AFKChannel?.Id)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerAfkChannelUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerAfkChannelChange(args.Id,
                        arsg2.AFKChannel?.Mention ?? strings.None(args.Id),
                        arsg2.AFKChannel?.Id.ToString() ?? "N/A", args.AFKChannel?.Mention ?? strings.None(args.Id),
                        args.AFKChannel?.Id.ToString() ?? "N/A", updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.AFKTimeout != arsg2.AFKTimeout)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerAfkTimeoutUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerAfkTimeoutChange(args.Id, arsg2.AFKTimeout, args.AFKTimeout,
                        updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.DefaultMessageNotifications != arsg2.DefaultMessageNotifications)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerDefaultNotificationsUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerDefaultNotificationsChange(args.Id,
                        arsg2.DefaultMessageNotifications,
                        args.DefaultMessageNotifications, updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.ExplicitContentFilter != arsg2.ExplicitContentFilter)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerExplicitContentFilterUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerExplicitContentFilterChange(args.Id,
                        arsg2.ExplicitContentFilter,
                        args.ExplicitContentFilter, updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.MfaLevel != arsg2.MfaLevel)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerMfaLevelUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerMfaLevelChange(args.Id, arsg2.MfaLevel, args.MfaLevel,
                        updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.VerificationLevel != arsg2.VerificationLevel)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerVerificationLevelUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerVerificationLevelChange(args.Id, arsg2.VerificationLevel,
                        args.VerificationLevel,
                        updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.SystemChannel?.Id != arsg2.SystemChannel?.Id)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerSystemChannelUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerSystemChannelChange(args.Id,
                        arsg2.SystemChannel?.Mention ?? strings.None(args.Id),
                        arsg2.SystemChannel?.Id.ToString() ?? "N/A",
                        args.SystemChannel?.Mention ?? strings.None(args.Id),
                        args.SystemChannel?.Id.ToString() ?? "N/A", updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.RulesChannel?.Id != arsg2.RulesChannel?.Id)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerRulesChannelUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerRulesChannelChange(args.Id,
                        arsg2.RulesChannel?.Mention ?? strings.None(args.Id),
                        arsg2.RulesChannel?.Id.ToString() ?? "N/A", args.RulesChannel?.Mention ?? strings.None(args.Id),
                        args.RulesChannel?.Id.ToString() ?? "N/A", updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.PublicUpdatesChannel?.Id != arsg2.PublicUpdatesChannel?.Id)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerPublicUpdatesChannelUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerPublicUpdatesChannelChange(args.Id,
                        arsg2.PublicUpdatesChannel?.Mention ?? strings.None(args.Id),
                        arsg2.PublicUpdatesChannel?.Id.ToString() ?? "N/A",
                        args.PublicUpdatesChannel?.Mention ?? strings.None(args.Id),
                        args.PublicUpdatesChannel?.Id.ToString() ?? "N/A", updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.MaxVideoChannelUsers != arsg2.MaxVideoChannelUsers)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerMaxVideoUsersUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerMaxVideoUsersChange(args.Id,
                        arsg2.MaxVideoChannelUsers?.ToString() ?? strings.Unlimited(args.Id),
                        args.MaxVideoChannelUsers?.ToString() ?? strings.Unlimited(args.Id), updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.MaxMembers != arsg2.MaxMembers)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ServerMaxMembersUpdated(args.Id)}"),
                    new TextDisplayBuilder(strings.ServerMaxMembersChange(args.Id,
                        arsg2.MaxMembers?.ToString() ?? "N/A",
                        args.MaxMembers?.ToString() ?? "N/A", updatedByStr))
                ], Mewdeko.OkColor);
            }
            else
                return;

            if (components.Components.Count > 0)
                await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                    allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a role is deleted in a guild.
    /// </summary>
    /// <param name="args">The role that was deleted.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnRoleDeleted(SocketRole args)
    {
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            if (logSetting.RoleDeletedId is null or 0)
                return;

            var channel = args.Guild.GetTextChannel(logSetting.RoleDeletedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await GetAuditLogsSafeAsync(args.Guild, ActionType.RoleDeleted);
            var auditLog = auditLogs.FirstOrDefault();
            if (auditLog == null) return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.RoleDeleted(args.Guild.Id)}"),
                    new TextDisplayBuilder(
                        strings.RoleField(args.Guild.Id, args.Name) +
                        strings.IdField(args.Guild.Id, args.Id) +
                        strings.DeletedByField(args.Guild.Id, auditLog.User.Mention, auditLog.User.Id) +
                        strings.DeletedAtField(args.Guild.Id, DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss")))
                ], Mewdeko.OkColor);

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a role is updated in a guild.
    /// </summary>
    /// <param name="args">The original role before the update.</param>
    /// <param name="arsg2">The updated role.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnRoleUpdated(SocketRole args, SocketRole arsg2)
    {
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            if (logSetting.RoleUpdatedId is null or 0)
                return;

            var channel = args.Guild.GetTextChannel(logSetting.RoleUpdatedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await GetAuditLogsSafeAsync(args.Guild, ActionType.RoleUpdated);
            var auditLog = auditLogs.FirstOrDefault();
            if (auditLog == null) return;

            var updatedByStr = $"`Updated By:` {auditLog.User.Mention} | {auditLog.User.Id}";
            var roleStr = $"`Role:` {args.Mention} | {args.Id}";
            var components = new ComponentBuilderV2();

            if (args.Name != arsg2.Name)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.RoleNameUpdated(args.Guild.Id)}"),
                    new TextDisplayBuilder(strings.RoleNameChange(args.Guild.Id, arsg2.Name, args.Name, updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.Colors.PrimaryColor != arsg2.Colors.PrimaryColor)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.RoleColorUpdated(args.Guild.Id)}"),
                    new TextDisplayBuilder(strings.RoleColorChange(args.Guild.Id, roleStr, arsg2.Colors.PrimaryColor,
                        args.Colors.PrimaryColor,
                        updatedByStr))
                ], arsg2.Colors.PrimaryColor);
            }
            else if (args.IsHoisted != arsg2.IsHoisted)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.RoleHoistedUpdated(args.Guild.Id)}"),
                    new TextDisplayBuilder(strings.RoleHoistedChange(args.Guild.Id, roleStr, arsg2.IsHoisted,
                        args.IsHoisted, updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.IsMentionable != arsg2.IsMentionable)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.RoleMentionableUpdated(args.Guild.Id)}"),
                    new TextDisplayBuilder(strings.RoleMentionableChange(args.Guild.Id, roleStr, arsg2.IsMentionable,
                        args.IsMentionable, updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.IsManaged != arsg2.IsManaged)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.RoleManagedUpdated(args.Guild.Id)}"),
                    new TextDisplayBuilder(strings.RoleManagedChange(args.Guild.Id, roleStr, arsg2.IsManaged,
                        args.IsManaged, updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.Position != arsg2.Position)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.RolePositionUpdated(args.Guild.Id)}"),
                    new TextDisplayBuilder(strings.RolePositionChange(args.Guild.Id, roleStr, arsg2.Position,
                        args.Position, updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (!arsg2.Permissions.Equals(args.Permissions))
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.RolePermissionsUpdated(args.Guild.Id)}"),
                    new TextDisplayBuilder(strings.RolePermissionsChange(args.Guild.Id, roleStr, arsg2.Permissions,
                        args.Permissions, updatedByStr))
                ], Mewdeko.OkColor);
            }
            else if (args.Icon != arsg2.Icon || args.Emoji?.ToString() != arsg2.Emoji?.ToString())
            {
                var iconUrl = arsg2.GetIconUrl() ?? args.GetIconUrl();
                components.WithSection([
                        new TextDisplayBuilder($"# {strings.RoleIconUpdated(args.Guild.Id)}"),
                        new TextDisplayBuilder(strings.RoleIconChange(args.Guild.Id, roleStr, updatedByStr))
                    ],
                    !string.IsNullOrEmpty(iconUrl)
                        ? new ThumbnailBuilder(new UnfurledMediaItemProperties(iconUrl))
                        : null);
            }
            else
                return; // No detectable change handled

            if (components.Components.Count > 0)
                await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                    allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a new event is created in a guild.
    /// </summary>
    /// <param name="args">The event that was created.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnEventCreated(SocketGuildEvent args)
    {
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            if (logSetting.EventCreatedId is null or 0)
                return;

            var channel = args.Guild.GetTextChannel(logSetting.EventCreatedId.Value);
            if (channel is null)
                return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.EventCreated(args.Guild.Id)}"),
                    new TextDisplayBuilder(
                        strings.EventField(args.GuildId, args.Name) +
                        $"`Created By:` {args.Creator?.Mention ?? "N/A"} | {args.Creator?.Id.ToString() ?? "N/A"}\n" +
                        $"`Created At:` {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}\n" +
                        $"`Description:` {args.Description}\n" +
                        $"`Event Date:` {args.StartTime:dd/MM/yyyy HH:mm:ss}\n" +
                        $"`End Date:` {args.EndTime:dd/MM/yyyy HH:mm:ss}\n" +
                        $"`Event Location:` {args.Location ?? "N/A"}\n" +
                        $"`Event Type:` {args.Type}\n" +
                        $"`Event Id:` {args.Id}")
                ], Mewdeko.OkColor);

            var coverImageUrl = args.GetCoverImageUrl();
            if (!string.IsNullOrEmpty(coverImageUrl))
                components.WithMediaGallery([
                    new MediaGalleryItemProperties
                    {
                        Media = new UnfurledMediaItemProperties(coverImageUrl)
                    }
                ]);

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles logging for when a thread is created.
    /// </summary>
    /// <param name="socketThreadChannel">The created thread channel.</param>
    private async Task OnThreadCreated(SocketThreadChannel socketThreadChannel)
    {
        // Check if thread or its parent channel is ignored
        if (IsChannelIgnored(socketThreadChannel.Guild.Id, socketThreadChannel.Id) ||
            (socketThreadChannel.ParentChannel != null &&
             IsChannelIgnored(socketThreadChannel.Guild.Id, socketThreadChannel.ParentChannel.Id)))
            return;

        if (GuildLogSettings.TryGetValue(socketThreadChannel.Guild.Id, out var logSetting))
        {
            if (logSetting.ThreadCreatedId is null or 0)
                return;

            if (socketThreadChannel.Guild.GetTextChannel(logSetting.ThreadCreatedId
                    .Value) is not IThreadChannel channel)
                return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.ThreadCreated(socketThreadChannel.Guild.Id)}"),
                    new TextDisplayBuilder(
                        strings.ThreadName(socketThreadChannel.Guild.Id, socketThreadChannel.Name) +
                        $"`Created By:` {socketThreadChannel.Owner?.Mention ?? "N/A"} | {socketThreadChannel.Owner?.Id.ToString() ?? "N/A"}\n" +
                        $"`Created At:` {socketThreadChannel.CreatedAt:dd/MM/yyyy HH:mm:ss}\n" +
                        $"`Thread Type:` {socketThreadChannel.Type}\n" +
                        $"`Thread Tags:` {string.Join(", ", socketThreadChannel.AppliedTags)}")
                ], Mewdeko.OkColor);

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a user has a role added to them.
    /// </summary>
    /// <param name="cacheable">The user before the event fired</param>
    /// <param name="arsg2">The user after the event was fired</param>
    private async Task OnUserRoleAdded(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser arsg2)
    {
        if (!GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting) || !cacheable.HasValue)
            return;

        if (logSetting.UserRoleAddedId is null or 0)
            return;

        var addedRoles = arsg2.Roles.Except(cacheable.Value.Roles).ToList();
        if (!addedRoles.Any()) return;

        var channel = arsg2.Guild.GetTextChannel(logSetting.UserRoleAddedId.Value);
        if (channel is null) return;

        await Task.Delay(500);
        var auditLogs = await GetAuditLogsSafeAsync(arsg2.Guild, ActionType.MemberRoleUpdated);
        var auditLog = auditLogs.LastOrDefault();
        if (auditLog?.User == null) return;

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {strings.UserRolesAdded(arsg2.Guild.Id)}"),
                new TextDisplayBuilder(
                    strings.RolesField(arsg2.Guild.Id,
                        string.Join(strings.CommaSeparator(arsg2.Guild.Id),
                            addedRoles.Select(x => x.Mention))) +
                    strings.AddedByField(arsg2.Guild.Id, auditLog.User.Mention, auditLog.User.Id) +
                    strings.AddedToField(arsg2.Guild.Id, arsg2.Mention, arsg2.Id))
            ], Mewdeko.OkColor);

        await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
            allowedMentions: AllowedMentions.None);
        collector.Feature("logging", channel.Guild.Id);
    }


    /// <summary>
    ///     Handles the event where a user has a role removed.
    /// </summary>
    /// <param name="cacheable">The user before the removal</param>
    /// <param name="arsg2">The user after the removal</param>
    private async Task OnUserRoleRemoved(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser arsg2)
    {
        if (!GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting) || !cacheable.HasValue)
            return;

        if (logSetting.UserRoleRemovedId is null or 0)
            return;

        var removedRoles = cacheable.Value.Roles.Except(arsg2.Roles).ToList();
        if (!removedRoles.Any()) return;

        var channel = arsg2.Guild.GetTextChannel(logSetting.UserRoleRemovedId.Value);
        if (channel is null) return;

        await Task.Delay(500);
        var auditLogs = await GetAuditLogsSafeAsync(arsg2.Guild, ActionType.MemberRoleUpdated);
        var auditLog = auditLogs.LastOrDefault();
        if (auditLog?.User == null) return;

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {strings.UserRolesRemoved(arsg2.Guild.Id)}"),
                new TextDisplayBuilder(
                    strings.RolesField(arsg2.Guild.Id,
                        string.Join(strings.CommaSeparator(arsg2.Guild.Id), removedRoles.Select(x => x.Mention))) +
                    strings.RemovedByField(arsg2.Guild.Id, auditLog.User.Mention, auditLog.User.Id) +
                    strings.RemovedFromField(arsg2.Guild.Id, arsg2.Mention, arsg2.Id))
            ], Mewdeko.OkColor);

        await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
            allowedMentions: AllowedMentions.None);
        collector.Feature("logging", channel.Guild.Id);
    }


    /// <summary>
    ///     Handles the event when a user updates their username.
    /// </summary>
    /// <param name="args">The user before they updated their username.</param>
    /// <param name="arsg2">The user after they updated their username.</param>
    private async Task OnUsernameUpdated(SocketUser args, SocketUser arsg2)
    {
        if (string.Equals(args.Username, arsg2.Username))
            return;

        // Find relevant guilds
        foreach (var guild in client.Guilds)
        {
            var user = guild.GetUser(args.Id);
            if (user != null && GuildLogSettings.TryGetValue(guild.Id, out var logSetting))
            {
                if (logSetting.UsernameUpdatedId is null or 0)
                    continue;

                var channel = guild.GetTextChannel(logSetting.UsernameUpdatedId.Value);
                if (channel is null)
                    continue;

                var components = new ComponentBuilderV2()
                    .WithContainer([
                        new TextDisplayBuilder($"# {strings.UsernameUpdated(guild.Id)}"),
                        new TextDisplayBuilder(
                            strings.UserField(guild.Id, user.Mention, user.Id) +
                            strings.OldUsernameField(guild.Id, args.Username) +
                            strings.NewUsernameField(guild.Id, arsg2.Username))
                    ], Mewdeko.OkColor);

                await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                    allowedMentions: AllowedMentions.None);
                collector.Feature("logging", channel.Guild.Id);
            }
        }
    }

    /// <summary>
    ///     Handles the event when a user updates their nickname.
    /// </summary>
    /// <param name="cacheable">The user before they updated their nickname</param>
    /// <param name="arsg2">The user after they updated their nickname</param>
    private async Task OnNicknameUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser arsg2)
    {
        if (!cacheable.HasValue || cacheable.Value.Nickname == arsg2.Nickname) return;

        if (GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting))
        {
            if (logSetting.NicknameUpdatedId is null or 0)
                return;

            var channel = arsg2.Guild.GetTextChannel(logSetting.NicknameUpdatedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await GetAuditLogsSafeAsync(arsg2.Guild, ActionType.MemberUpdated);
            var entry = auditLogs.FirstOrDefault();
            if (entry == null) return; // Cannot determine who changed it without audit log

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.NicknameUpdated(arsg2.Guild.Id)}"),
                    new TextDisplayBuilder(
                        strings.UserField(arsg2.Guild.Id, arsg2.Mention, arsg2.Id) +
                        strings.OldNicknameField(arsg2.Guild.Id, cacheable.Value.Nickname ?? cacheable.Value.Username) +
                        strings.NewNicknameField(arsg2.Guild.Id, arsg2.Nickname ?? arsg2.Username) +
                        strings.UpdatedByField(arsg2.Guild.Id, entry.User.Mention, entry.User.Id))
                ], Mewdeko.OkColor);

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a thread gets deleted.
    /// </summary>
    /// <param name="args">The cached thread. May return null. See <see cref="Cacheable{TEntity,TId}" /></param>
    private async Task OnThreadDeleted(Cacheable<SocketThreadChannel, ulong> args)
    {
        if (!args.HasValue) return;
        var deletedThread = args.Value;

        // Check if thread or its parent channel is ignored
        if (IsChannelIgnored(deletedThread.Guild.Id, deletedThread.Id) ||
            (deletedThread.ParentChannel != null &&
             IsChannelIgnored(deletedThread.Guild.Id, deletedThread.ParentChannel.Id)))
            return;

        if (GuildLogSettings.TryGetValue(deletedThread.Guild.Id, out var logSetting))
        {
            if (logSetting.ThreadDeletedId is null or 0)
                return;

            var channel = deletedThread.Guild.GetTextChannel(logSetting.ThreadDeletedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await GetAuditLogsSafeAsync(deletedThread.Guild, ActionType.ThreadDelete);
            var entry = auditLogs.FirstOrDefault();
            if (entry == null) return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.ThreadDeleted(deletedThread.Guild.Id)}")
                ], Mewdeko.OkColor);

            components.WithSeparator();
            components.WithTextDisplay("\n**Thread Information**");
            components.WithTextDisplay(
                strings.ThreadNameField(deletedThread.Guild.Id, deletedThread.Name) +
                strings.ThreadIdField(deletedThread.Guild.Id, deletedThread.Id));

            components.WithSeparator();
            components.WithTextDisplay("\n**Deletion Details**");
            components.WithTextDisplay(
                strings.DeletedByField(deletedThread.Guild.Id, entry.User.Mention, entry.User.Id));

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a thread is updated.
    /// </summary>
    /// <param name="cacheable">The cached thread. May return null. See <see cref="Cacheable{TEntity,TId}" /></param>
    /// <param name="arsg2">The updated thread.</param>
    private async Task OnThreadUpdated(Cacheable<SocketThreadChannel, ulong> cacheable, SocketThreadChannel arsg2)
    {
        if (!cacheable.HasValue) return;
        var oldThread = cacheable.Value;

        // Check if thread or its parent channel is ignored
        if (IsChannelIgnored(arsg2.Guild.Id, arsg2.Id) ||
            (arsg2.ParentChannel != null && IsChannelIgnored(arsg2.Guild.Id, arsg2.ParentChannel.Id)))
            return;

        if (GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting))
        {
            if (logSetting.ThreadUpdatedId is null or 0)
                return;

            var channel = arsg2.Guild.GetTextChannel(logSetting.ThreadUpdatedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await GetAuditLogsSafeAsync(arsg2.Guild, ActionType.ThreadUpdate);
            var entry = auditLogs.FirstOrDefault();
            if (entry == null) return;

            var updatedByStr = $"`Updated By:` {entry.User.Mention} | {entry.User.Id}";
            var threadIdStr = $"`Thread:` {arsg2.Mention} | {arsg2.Id}";
            var components = new ComponentBuilderV2();

            if (oldThread.Name != arsg2.Name)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ThreadNameUpdated(arsg2.Guild.Id)}")
                ], Mewdeko.OkColor);
                components.WithSeparator();
                components.WithTextDisplay("\n**Change Details**");
                components.WithTextDisplay(strings.ThreadNameChange(arsg2.Guild.Id, threadIdStr, oldThread.Name,
                    arsg2.Name, updatedByStr));
            }
            else if (oldThread.IsArchived != arsg2.IsArchived)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ThreadArchiveStatusUpdated(arsg2.Guild.Id)}")
                ], Mewdeko.OkColor);
                components.WithSeparator();
                components.WithTextDisplay("\n**Archive Status Change**");
                components.WithTextDisplay(strings.ThreadArchiveStatusChange(arsg2.Guild.Id, threadIdStr,
                    oldThread.IsArchived,
                    arsg2.IsArchived, updatedByStr));
            }
            else if (oldThread.IsLocked != arsg2.IsLocked)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ThreadLockStatusUpdated(arsg2.Guild.Id)}")
                ], Mewdeko.OkColor);
                components.WithSeparator();
                components.WithTextDisplay("\n**Lock Status Change**");
                components.WithTextDisplay(strings.ThreadLockStatusChange(arsg2.Guild.Id, threadIdStr,
                    oldThread.IsLocked, arsg2.IsLocked,
                    updatedByStr));
            }
            else if (oldThread.SlowModeInterval != arsg2.SlowModeInterval)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ThreadSlowModeUpdated(arsg2.Guild.Id)}")
                ], Mewdeko.OkColor);
                components.WithSeparator();
                components.WithTextDisplay("\n**Slow Mode Change**");
                components.WithTextDisplay(strings.ThreadSlowModeChange(arsg2.Guild.Id, threadIdStr,
                    oldThread.SlowModeInterval,
                    arsg2.SlowModeInterval, updatedByStr));
            }
            else if (oldThread.AutoArchiveDuration != arsg2.AutoArchiveDuration)
            {
                components.WithContainer([
                    new TextDisplayBuilder($"# {strings.ThreadAutoArchiveUpdated(arsg2.Guild.Id)}")
                ], Mewdeko.OkColor);
                components.WithSeparator();
                components.WithTextDisplay("\n**Auto Archive Duration Change**");
                components.WithTextDisplay(strings.ThreadAutoArchiveChange(arsg2.Guild.Id, threadIdStr,
                    oldThread.AutoArchiveDuration,
                    arsg2.AutoArchiveDuration, updatedByStr));
            }
            else
                return; // No detectable change handled

            if (components.Components.Count > 0)
                await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                    allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a message is updated.
    /// </summary>
    /// <param name="cacheable">The cached message. May return null. See <see cref="Cacheable{TEntity,TId}" /></param>
    /// <param name="args2">The new message</param>
    /// <param name="args3">The channel where the message was updated</param>
    private async Task OnMessageUpdated(Cacheable<IMessage, ulong> cacheable, SocketMessage args2,
        ISocketMessageChannel args3)
    {
        if (!cacheable.HasValue) return;
        var oldMessage = cacheable.Value;
        if (args3 is not SocketTextChannel guildChannel || IgnoresBot(guildChannel.Guild.Id, args2.Author)) return;
        if (oldMessage.Content == args2.Content) return; // Compare content directly

        // Check if channel is ignored
        if (IsChannelIgnored(guildChannel.Guild.Id, guildChannel.Id))
            return;

        if (GuildLogSettings.TryGetValue(guildChannel.Guild.Id, out var logSetting))
        {
            if (logSetting.MessageUpdatedId is null or 0)
                return;

            if (StartsWithIgnoredPrefix(logSetting, oldMessage.Content) ||
                StartsWithIgnoredPrefix(logSetting, args2.Content))
                return;

            var channel = guildChannel.Guild.GetTextChannel(logSetting.MessageUpdatedId.Value);
            if (channel is null)
                return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.MessageUpdated(guildChannel.Guild.Id)}")
                ], Mewdeko.OkColor);

            // Message details with author avatar
            var avatarUrl = oldMessage.Author.RealAvatarUrl().ToString();
            var messageDetails =
                strings.MessageAuthorField(guildChannel.Guild.Id, oldMessage.Author.Mention, oldMessage.Author.Id) +
                strings.MessageChannelField(guildChannel.Guild.Id, guildChannel.Mention, guildChannel.Id) +
                strings.MessageIdField(guildChannel.Guild.Id, oldMessage.Id);

            if (!string.IsNullOrEmpty(avatarUrl))
            {
                components.WithSection([
                    new TextDisplayBuilder("**Message Details**"),
                    new TextDisplayBuilder(messageDetails)
                ], new ThumbnailBuilder(new UnfurledMediaItemProperties(avatarUrl)));
            }
            else
            {
                components.WithSeparator();
                components.WithTextDisplay("\n**Message Details**");
                components.WithTextDisplay(messageDetails);
            }

            // Before/After content comparison
            components.WithSeparator();
            components.WithTextDisplay("\n**Content Changes**");

            if (!string.IsNullOrWhiteSpace(oldMessage.Content))
            {
                components.WithContainer([
                    new TextDisplayBuilder("**Before**"),
                    new TextDisplayBuilder(strings.OldMessageContentField(guildChannel.Guild.Id,
                        oldMessage.Content.TrimTo(500)))
                ], null);
            }

            if (!string.IsNullOrWhiteSpace(args2.Content))
            {
                components.WithContainer([
                    new TextDisplayBuilder("**After**"),
                    new TextDisplayBuilder(
                        strings.UpdatedMessageContentField(guildChannel.Guild.Id, args2.Content.TrimTo(500)))
                ], null);
            }

            components.WithActionRow([
                new ButtonBuilder("Jump to Message", style: ButtonStyle.Link, url: oldMessage.GetJumpUrl())
            ]);

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a message is deleted.
    /// </summary>
    /// <param name="args">The cached message. May return null. See <see cref="Cacheable{TEntity,TId}" /></param>
    /// <param name="arsg2">The channel where the message was deleted</param>
    private async Task OnMessageDeleted(Cacheable<IMessage, ulong> args, Cacheable<IMessageChannel, ulong> arsg2)
    {
        // Get message from cache if available
        var message = args.HasValue ? args.Value : null;
        // If message not in cache, we cannot proceed as we don't have content/author
        if (message == null) return;
        // Get channel from cache if available
        var messageChannel = arsg2.HasValue ? arsg2.Value : null;
        if (messageChannel is not SocketTextChannel guildChannel) return; // Ensure it's a guild channel
        if (IgnoresBot(guildChannel.Guild.Id, message.Author)) return;

        // Check if channel is ignored
        if (IsChannelIgnored(guildChannel.Guild.Id, guildChannel.Id))
            return;

        if (GuildLogSettings.TryGetValue(guildChannel.Guild.Id, out var logSetting))
        {
            if (logSetting.MessageDeletedId is null or 0)
                return;

            if (StartsWithIgnoredPrefix(logSetting, message.Content))
                return;

            var logChannel = guildChannel.Guild.GetTextChannel(logSetting.MessageDeletedId.Value);
            if (logChannel is null)
                return;

            await Task.Delay(1000); // Delay slightly to increase chance of audit log availability

            var auditLogs = await GetAuditLogsSafeAsync(guildChannel.Guild, ActionType.MessageDeleted, 5);

            // Try to find who deleted the message
            var deleteUser = message.Author; // Assume self-delete initially
            var entry = auditLogs.FirstOrDefault(e =>
                e.Data is MessageDeleteAuditLogData data && data.Target.Id == message.Author.Id);

            if (entry != null &&
                (DateTimeOffset.UtcNow - entry.CreatedAt).TotalSeconds < 10) // Check if recent audit log matches
            {
                deleteUser = entry.User;
            }


            var components = new ComponentBuilderV2();
            var tempFiles = new List<FileAttachment>();

            // Build the main content with better organization
            components.WithContainer([
                new TextDisplayBuilder($"# {strings.MessageDeleted(guildChannel.Guild.Id)}")
            ], Mewdeko.OkColor);

            // Message details section with author avatar as thumbnail
            var avatarUrl = message.Author.RealAvatarUrl().ToString();
            var messageDetails =
                strings.MessageAuthorField(guildChannel.Guild.Id, message.Author.Mention, message.Author.Id) +
                strings.MessageChannelField(guildChannel.Guild.Id, guildChannel.Mention, guildChannel.Id) +
                strings.DeletedByField(guildChannel.Guild.Id,
                    deleteUser?.Mention ?? strings.Unknown(guildChannel.Guild.Id),
                    deleteUser?.Id.ToString() ?? "N/A");

            if (!string.IsNullOrEmpty(avatarUrl))
            {
                components.WithSection([
                    new TextDisplayBuilder("**Message Details**"),
                    new TextDisplayBuilder(messageDetails)
                ], new ThumbnailBuilder(new UnfurledMediaItemProperties(avatarUrl)));
            }
            else
            {
                components.WithSeparator();
                components.WithTextDisplay("**Message Details**");
                components.WithTextDisplay(messageDetails);
            }

            // Content section if message had content
            if (!string.IsNullOrWhiteSpace(message.Content))
            {
                components.WithSeparator();
                components.WithContainer([
                    new TextDisplayBuilder("**Message Content**"),
                    new TextDisplayBuilder(message.Content.TrimTo(1000))
                ], null);
            }

            // Handle attachments with temporary download for cached content
            if (message.Attachments.Count > 0)
            {
                var mediaItems = new List<MediaGalleryItemProperties>();
                var fileComponents = new List<FileComponentBuilder>();

                components.WithSeparator();
                components.WithTextDisplay(
                    $"**{strings.Attachments(guildChannel.Guild.Id)}** ({message.Attachments.Count})");

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                foreach (var att in message.Attachments.Take(10)) // Limit to 10 for performance
                {
                    try
                    {
                        // Download the cached attachment temporarily
                        var response = await httpClient.GetAsync(att.Url);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsByteArrayAsync();
                            var stream = new MemoryStream(content);
                            var fileAttachment = new FileAttachment(stream, att.Filename, att.Filename);
                            tempFiles.Add(fileAttachment);

                            if (IsImageAttachment(att.Filename))
                            {
                                // Add to media gallery for images
                                mediaItems.Add(new MediaGalleryItemProperties
                                {
                                    Media = new UnfurledMediaItemProperties($"attachment://{att.Filename}"),
                                    Description = $"{att.Filename} ({(att.Size / 1024.0):F1} KB)"
                                });
                            }
                            else
                            {
                                // Add as file component for non-images
                                fileComponents.Add(new FileComponentBuilder
                                {
                                    File = new UnfurledMediaItemProperties($"attachment://{att.Filename}")
                                });
                            }
                        }
                    }
                    catch
                    {
                        // If download fails, just show as text
                        components.WithTextDisplay(
                            $"📎 **{att.Filename}** ({strings.AttachmentSize(guildChannel.Guild.Id, att.Size)}) - *Could not preserve attachment*");
                    }
                }

                // Add media gallery if we have images
                if (mediaItems.Count > 0)
                {
                    components.WithMediaGallery(mediaItems);
                }

                // Add file components for non-images
                foreach (var fileComp in fileComponents)
                {
                    components.AddComponent(fileComp);
                }
            }

            try
            {
                if (tempFiles.Count > 0)
                {
                    await logChannel.SendFilesAsync(
                        tempFiles,
                        components: components.Build(),
                        flags: MessageFlags.ComponentsV2,
                        allowedMentions: AllowedMentions.None);
                }
                else
                {
                    await logChannel.SendMessageAsync(
                        components: components.Build(),
                        flags: MessageFlags.ComponentsV2,
                        allowedMentions: AllowedMentions.None);
                    collector.Feature("logging", logChannel.Guild.Id);
                }
            }
            finally
            {
                // Clean up temporary files
                foreach (var tempFile in tempFiles)
                {
                    tempFile.Dispose();
                }
            }
        }
    }

    private static bool IsImageAttachment(string filename)
    {
        var imageExtensions = new[]
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
        }; // Added webp
        return imageExtensions.Any(ext => filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Handles the event when a user joins a guild.
    /// </summary>
    /// <param name="guildUser">The user that joined the guild.</param>
    private async Task OnUserJoined(IGuildUser guildUser)
    {
        logger.LogDebug("LogCommandService.OnUserJoined called for user {UserId} in guild {GuildId}", guildUser.Id,
            guildUser.Guild.Id);
        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.UserJoinedId is null or 0)
                return;

            var channel = await guildUser.Guild.GetTextChannelAsync(logSetting.UserJoinedId.Value);
            if (channel is null)
                return;

            var avatarUrl = guildUser.RealAvatarUrl().ToString();
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.UserJoined(guildUser.Guild.Id)}")
                ], Mewdeko.OkColor);

            // User info section with avatar
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                components.WithSection([
                    new TextDisplayBuilder("**User Information**"),
                    new TextDisplayBuilder(strings.UserField(guildUser.Guild.Id, guildUser.Mention, guildUser.Id) +
                                           strings.UserGlobalNameField(guildUser.Guild.Id,
                                               guildUser.GlobalName ?? guildUser.Username))
                ], new ThumbnailBuilder(new UnfurledMediaItemProperties(avatarUrl)));
            }
            else
            {
                components.WithSeparator();
                components.WithTextDisplay("**User Information**");
                components.WithTextDisplay(strings.UserField(guildUser.Guild.Id, guildUser.Mention, guildUser.Id) +
                                           strings.UserGlobalNameField(guildUser.Guild.Id,
                                               guildUser.GlobalName ?? guildUser.Username));
            }

            // Account details
            components.WithSeparator();
            components.WithTextDisplay("\n**Account Details**");
            components.WithTextDisplay(
                strings.AccountCreatedField(guildUser.Guild.Id, guildUser.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss")) +
                strings.JoinedServerField(guildUser.Guild.Id,
                    guildUser.JoinedAt?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A") +
                strings.UserStatusField(guildUser.Guild.Id, guildUser.Status));

            components.WithActionRow([
                new ButtonBuilder(strings.ViewUser(guildUser.Guild.Id), style: ButtonStyle.Link,
                    url: $"discord://-/users/{guildUser.Id}")
            ]);

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a user leaves a guild.
    /// </summary>
    /// <param name="guild">The guild the user left.</param>
    /// <param name="user">The user that left the guild.</param>
    private async Task OnUserLeft(IGuild guild, IUser user)
    {
        if (GuildLogSettings.TryGetValue(guild.Id, out var logSetting))
        {
            if (logSetting.UserLeftId is null or 0)
                return;

            var channel = await guild.GetTextChannelAsync(logSetting.UserLeftId.Value);
            if (channel is null)
                return;

            // Check if it was a kick or ban via audit log
            var title = "User Left";
            string footer = null;
            await Task.Delay(1000); // Delay for audit log
            var auditLogsKick = await GetAuditLogsSafeAsync(guild, ActionType.Kick);
            var kickLog = auditLogsKick.FirstOrDefault(e =>
                e.Data is KickAuditLogData data && data.Target.Id == user.Id &&
                (DateTimeOffset.UtcNow - e.CreatedAt).TotalSeconds < 10);

            var auditLogsBan = await GetAuditLogsSafeAsync(guild, ActionType.Ban);
            var banLog = auditLogsBan.FirstOrDefault(e =>
                e.Data is BanAuditLogData data && data.Target.Id == user.Id &&
                (DateTimeOffset.UtcNow - e.CreatedAt).TotalSeconds < 10);

            if (kickLog != null)
            {
                title = "User Kicked";
                footer = $"Kicked by {kickLog.User.Username} ({kickLog.User.Id})";
            }
            else if (banLog != null)
            {
                // Ban event is handled by OnUserBanned, so we might not want to log here again.
                // However, if the user left *before* the ban event fired, this might catch it.
                // Let's just stick to "User Left" unless kicked.
            }

            var avatarUrl = user.RealAvatarUrl().ToString();
            var userInfo = strings.UserField(guild.Id, user.Mention, user.Id) +
                           strings.UserGlobalNameField(guild.Id, user.GlobalName ?? user.Username);

            var accountInfo = strings.AccountCreatedField(guild.Id, user.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"));

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {title}")
                ], Mewdeko.ErrorColor);

            // User info section with avatar
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                components.WithSection([
                    new TextDisplayBuilder("**User Information**"),
                    new TextDisplayBuilder(userInfo)
                ], new ThumbnailBuilder(new UnfurledMediaItemProperties(avatarUrl)));
            }
            else
            {
                components.WithSeparator();
                components.WithTextDisplay("\n**User Information**");
                components.WithTextDisplay(userInfo);
            }

            // Account details
            components.WithSeparator();
            components.WithTextDisplay("\n**Account Details**");
            components.WithTextDisplay(accountInfo);

            // Footer with kick/ban info if available
            if (footer != null)
            {
                components.WithSeparator();
                components.WithTextDisplay($"\n*{footer}*");
            }

            components.WithActionRow([
                new ButtonBuilder(strings.ViewUserMayNotWork(guild.Id), style: ButtonStyle.Link,
                    url: $"discord://-/users/{user.Id}")
            ]);

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a user is banned from a guild.
    /// </summary>
    /// <param name="user">The user that was banned.</param>
    /// <param name="guild">The guild the user was banned from.</param>
    /// <param name="bannedBy">The user that banned the user.</param>
    /// <param name="reason">The reason for the ban.</param>
    private async Task OnUserBanned(IUser user, SocketGuild guild, SocketUser bannedBy, string? reason = null)
    {
        if (GuildLogSettings.TryGetValue(guild.Id, out var logSetting))
        {
            if (logSetting.UserBannedId is null or 0)
                return;

            var channel = guild.GetTextChannel(logSetting.UserBannedId.Value);
            if (channel is null)
                return;

            var avatarUrl = user.RealAvatarUrl().ToString();
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.UserBanned(guild.Id)}"),
                    new TextDisplayBuilder(
                        strings.UserInfoLine(guild.Id, user.Mention, user.Id) +
                        $"`User Global Name:` {user.GlobalName ?? user.Username}\n" +
                        $"`Account Created:` {user.CreatedAt:dd/MM/yyyy HH:mm:ss}\n" +
                        $"`Banned By:` {bannedBy?.Mention ?? "Unknown"} | {bannedBy?.Id.ToString() ?? "N/A"}\n" +
                        $"`Reason:` {reason ?? "No reason provided"}")
                ], Mewdeko.ErrorColor);

            if (!string.IsNullOrEmpty(avatarUrl))
                components.WithMediaGallery([
                    new MediaGalleryItemProperties
                    {
                        Media = new UnfurledMediaItemProperties(avatarUrl)
                    }
                ]);

            components.WithActionRow([
                new ButtonBuilder("View User (May not work)", style: ButtonStyle.Link,
                    url: $"discord://-/users/{user.Id}")
            ]);

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a user is unbanned from a guild.
    /// </summary>
    /// <param name="user">The user that was unbanned.</param>
    /// <param name="guild">The guild the user was unbanned from.</param>
    /// <param name="unbannedBy">The user that unbanned the user.</param>
    /// <param name="reason">The reason for the unban, if provided.</param>
    private async Task OnUserUnbanned(IUser user, SocketGuild guild, SocketUser unbannedBy, string reason)
    {
        if (GuildLogSettings.TryGetValue(guild.Id, out var logSetting))
        {
            if (logSetting.UserUnbannedId is null or 0)
                return;

            var channel = guild.GetTextChannel(logSetting.UserUnbannedId.Value);
            if (channel is null)
                return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.UserUnbanned(guild.Id)}")
                ], Mewdeko.OkColor)
                .WithSeparator()
                .WithSection([
                    new TextDisplayBuilder($"**User Details**\n{strings.UserInfoLine(guild.Id, user.Mention, user.Id)}")
                ], new ThumbnailBuilder(user.RealAvatarUrl().ToString()))
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder($"**Additional Information**\n" +
                                                      $"`User Global Name:` {user.GlobalName ?? user.Username}\n" +
                                                      $"`Account Created:` {user.CreatedAt:dd/MM/yyyy HH:mm:ss}\n" +
                                                      $"`Unbanned By:` {unbannedBy?.Mention ?? "Unknown"} | {unbannedBy?.Id.ToString() ?? "N/A"}" +
                                                      $"`Reason:` {reason ?? "No reason provided"}"))
                .WithActionRow([
                    new ButtonBuilder("View User (May not work)", style: ButtonStyle.Link,
                        url: $"discord://-/users/{user.Id}")
                ]);

            await channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a user is updated.
    /// </summary>
    /// <param name="args">The user before the update.</param>
    /// <param name="arsg2">The user after the update.</param>
    private async Task OnUserUpdated(SocketUser args, SocketUser arsg2)
    {
        var hasAvatarChanged = args.AvatarId != arsg2.AvatarId;
        var hasGlobalNameChanged = args.GlobalName != arsg2.GlobalName;

        if (!hasAvatarChanged && !hasGlobalNameChanged) return;

        // Iterate through shared guilds
        foreach (var guild in client.Guilds)
        {
            var userInGuild = guild.GetUser(args.Id);
            if (userInGuild == null || !GuildLogSettings.TryGetValue(guild.Id, out var logSetting)) continue;
            if (logSetting.UserUpdatedId is null or 0 || IgnoresBot(guild.Id, args))
                continue;

            var logChannel = guild.GetTextChannel(logSetting.UserUpdatedId.Value);
            if (logChannel is null)
                continue;

            if (hasAvatarChanged)
            {
                if (logSetting.AvatarUpdatedId != null)
                    logChannel = guild.GetTextChannel(logSetting.AvatarUpdatedId.Value);
                if (logChannel is null)
                    return;

                var avatarComponents = new ComponentBuilderV2()
                    .WithContainer([
                        new TextDisplayBuilder($"# {strings.UserAvatarUpdated(guild.Id)}")
                    ], Mewdeko.OkColor)
                    .WithSeparator()
                    .WithContainer(new TextDisplayBuilder(
                        $"**User Details**\n{strings.UserLogEntry(guild.Id, userInGuild.Mention, userInGuild.Id)}"))
                    .WithSeparator()
                    .WithMediaGallery([
                        args.RealAvatarUrl().ToString(),
                        arsg2.RealAvatarUrl().ToString()
                    ]);

                await logChannel.SendMessageAsync(components: avatarComponents.Build(),
                    flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
                collector.Feature("logging", logChannel.Guild.Id);
            }

            if (!hasGlobalNameChanged) continue;
            if (logSetting.UsernameUpdatedId != null)
                logChannel = guild.GetTextChannel(logSetting.UsernameUpdatedId.Value);
            if (logChannel is null)
                return;
            var globalNameComponents = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.UserGlobalNameUpdated(guild.Id)}")
                ], Mewdeko.OkColor)
                .WithSeparator()
                .WithSection([
                    new TextDisplayBuilder(
                        $"**User Details**\n{strings.UserLogEntry(guild.Id, userInGuild.Mention, userInGuild.Id)}")
                ], new ThumbnailBuilder(arsg2.RealAvatarUrl().ToString()))
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder($"**Name Changes**\n" +
                                                      $"`Old Global Name:` {args.GlobalName ?? args.Username}\n" +
                                                      $"`New Global Name:` {arsg2.GlobalName ?? arsg2.Username}"));

            await logChannel.SendMessageAsync(components: globalNameComponents.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
            collector.Feature("logging", logChannel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a channel is created in a guild.
    /// </summary>
    /// <param name="args">The channel that was created.</param>
    private async Task OnChannelCreated(SocketChannel args)
    {
        if (args is not SocketGuildChannel channel) return;

        // Check if channel is ignored
        if (IsChannelIgnored(channel.Guild.Id, channel.Id))
            return;

        if (GuildLogSettings.TryGetValue(channel.Guild.Id, out var logSetting))
        {
            if (logSetting.ChannelCreatedId is null or 0)
                return;

            var logChannel = channel.Guild.GetTextChannel(logSetting.ChannelCreatedId.Value);
            if (logChannel is null)
                return;

            var createdType = channel switch
            {
                SocketStageChannel => "Stage",
                SocketVoiceChannel => "Voice",
                SocketNewsChannel => "News",
                SocketTextChannel => "Text",
                SocketCategoryChannel => "Category",
                _ => "Unknown"
            };

            await Task.Delay(500);
            var auditLogs = await GetAuditLogsSafeAsync(channel.Guild, ActionType.ChannelCreated);
            var entry = auditLogs.FirstOrDefault();

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.ChannelCreated(channel.Guild.Id)}")
                ], Mewdeko.OkColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder($"**Channel Details**\n" +
                                                      $"`Channel:` <#{channel.Id}> ({channel.Name}) | {channel.Id}\n" +
                                                      $"`Channel Type:` {createdType}"))
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder($"**Creation Information**\n" +
                                                      $"`Created By:` {entry?.User.Mention ?? "Unknown"} | {entry?.User.Id.ToString() ?? "N/A"}\n" +
                                                      $"`Created At:` {channel.CreatedAt:dd/MM/yyyy HH:mm:ss}"));

            await logChannel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
            collector.Feature("logging", logChannel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a channel is destroyed/deleted in a guild.
    /// </summary>
    /// <param name="args">The channel that was destroyed.</param>
    private async Task OnChannelDestroyed(SocketChannel args)
    {
        if (args is not SocketGuildChannel channel) return;

        // Check if channel is ignored
        if (IsChannelIgnored(channel.Guild.Id, channel.Id))
            return;

        if (GuildLogSettings.TryGetValue(channel.Guild.Id, out var logSetting))
        {
            if (logSetting.ChannelDestroyedId is null or 0)
                return;

            var logChannel = channel.Guild.GetTextChannel(logSetting.ChannelDestroyedId.Value);
            if (logChannel is null)
                return;

            var createdType = channel switch
            {
                SocketStageChannel => "Stage",
                SocketVoiceChannel => "Voice",
                SocketNewsChannel => "News",
                SocketTextChannel => "Text",
                SocketCategoryChannel => "Category",
                _ => "Unknown"
            };

            await Task.Delay(500);
            var auditLogs = await GetAuditLogsSafeAsync(channel.Guild, ActionType.ChannelDeleted);
            var entry = auditLogs.FirstOrDefault();

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.ChannelDestroyed(channel.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder($"**Channel Details**\n" +
                                                      $"{strings.ChannelLogEntry(channel.Guild.Id, channel.Name, channel.Id)}\n" +
                                                      $"`Channel Type:` {createdType}"))
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder($"**Deletion Information**\n" +
                                                      $"`Destroyed By:` {entry?.User.Mention ?? "Unknown"} | {entry?.User.Id.ToString() ?? "N/A"}\n" +
                                                      $"`Destroyed At:` {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}"));

            await logChannel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
            collector.Feature("logging", logChannel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a channel is updated in a guild.
    /// </summary>
    /// <param name="args">The channel before the update.</param>
    /// <param name="arsg2">The channel after the update.</param>
    private async Task OnChannelUpdated(SocketChannel args, SocketChannel arsg2)
    {
        if (args is not SocketGuildChannel channel || arsg2 is not SocketGuildChannel channel2) return;
        if (channel.Id != channel2.Id) return; // Should be the same channel

        // Check if channel is ignored
        if (IsChannelIgnored(channel.Guild.Id, channel.Id))
            return;

        if (GuildLogSettings.TryGetValue(channel.Guild.Id, out var logSetting))
        {
            if (logSetting.ChannelUpdatedId is null or 0)
                return;

            var logChannel = channel.Guild.GetTextChannel(logSetting.ChannelUpdatedId.Value);
            if (logChannel is null)
                return;

            await Task.Delay(500);
            var audit = await GetAuditLogsSafeAsync(channel.Guild, ActionType.ChannelUpdated);
            var entry = audit.FirstOrDefault();
            if (entry == null) return;

            var updatedByStr = $"`Updated By:` {entry.User.Mention} | {entry.User.Id}";
            var channelIdStr =
                $"`Channel:` <#{channel2.Id}> ({channel2.Name}) | {channel2.Id}"; // Mention current state
            var changed = false;
            var title = "";
            var description = "";

            if (channel.Name != channel2.Name)
            {
                title = strings.ChannelNameUpdated(channel.Guild.Id);
                description = strings.ChannelNameChange(channel.Guild.Id, channelIdStr, channel.Name, channel2.Name,
                    updatedByStr);
                changed = true;
            }
            else if (channel.Position != channel2.Position)
            {
                // Position changes are often noisy due to category changes, maybe ignore?
                // eb.WithTitle(strings.ChannelPositionUpdated(channel.Guild.Id)).WithDescription(strings.ChannelPositionChange(channel.Guild.Id, channelIdStr, channel.Position, channel2.Position, updatedByStr));
                // changed = true;
            }

            switch (channel)
            {
                // Text Channel specific changes
                case SocketTextChannel textChannel when channel2 is SocketTextChannel textChannel2:
                {
                    if (textChannel.Topic != textChannel2.Topic)
                    {
                        title = strings.ChannelTopicUpdated(channel.Guild.Id);
                        description = strings.ChannelTopicChange(channel.Guild.Id, channelIdStr,
                            textChannel.Topic?.TrimTo(100) ?? strings.None(channel.Guild.Id),
                            textChannel2.Topic?.TrimTo(100) ?? strings.None(channel.Guild.Id), updatedByStr);
                        changed = true;
                    }
                    else if (textChannel.IsNsfw != textChannel2.IsNsfw)
                    {
                        title = strings.ChannelNsfwUpdated(channel.Guild.Id);
                        description = strings.ChannelNsfwChange(channel.Guild.Id, channelIdStr, textChannel.IsNsfw,
                            textChannel2.IsNsfw, updatedByStr);
                        changed = true;
                    }
                    else if (textChannel.SlowModeInterval != textChannel2.SlowModeInterval)
                    {
                        title = strings.ChannelSlowmodeUpdated(channel.Guild.Id);
                        description = strings.ChannelSlowmodeChange(channel.Guild.Id, channelIdStr,
                            textChannel.SlowModeInterval,
                            textChannel2.SlowModeInterval, updatedByStr);
                        changed = true;
                    }
                    else if (textChannel.CategoryId != textChannel2.CategoryId)
                    {
                        title = strings.ChannelCategoryUpdated(channel.Guild.Id);
                        description = strings.ChannelCategoryChange(channel.Guild.Id, channelIdStr,
                            textChannel.Category?.Name ?? strings.None(channel.Guild.Id),
                            textChannel2.Category?.Name ?? strings.None(channel.Guild.Id), updatedByStr);
                        changed = true;
                    }

                    break;
                }
                // Voice Channel specific changes
                case IVoiceChannel voiceChannel when channel2 is IVoiceChannel voiceChannel2:
                {
                    if (voiceChannel.Bitrate != voiceChannel2.Bitrate)
                    {
                        title = strings.ChannelBitrateUpdated(channel.Guild.Id);
                        description = strings.ChannelBitrateChange(channel.Guild.Id, channelIdStr,
                            voiceChannel.Bitrate / 1000,
                            voiceChannel2.Bitrate / 1000, updatedByStr);
                        changed = true;
                    }
                    else if (voiceChannel.UserLimit != voiceChannel2.UserLimit)
                    {
                        title = strings.ChannelUserLimitUpdated(channel.Guild.Id);
                        description = strings.ChannelUserLimitChange(channel.Guild.Id, channelIdStr,
                            voiceChannel.UserLimit?.ToString() ?? strings.Unlimited(channel.Guild.Id),
                            voiceChannel2.UserLimit?.ToString() ?? strings.Unlimited(channel.Guild.Id), updatedByStr);
                        changed = true;
                    }
                    else if (voiceChannel.CategoryId != voiceChannel2.CategoryId)
                    {
                        title = strings.ChannelCategoryUpdated(channel.Guild.Id);
                        description =
                            $"{channelIdStr}\n`Old Category:` {(await voiceChannel.GetCategoryAsync())?.Name ?? strings.None(channel.Guild.Id)}\n`New Category:` {(await voiceChannel2.GetCategoryAsync())?.Name ?? strings.None(channel.Guild.Id)}\n{updatedByStr}";
                        changed = true;
                    }
                    else if (voiceChannel.VideoQualityMode != voiceChannel2.VideoQualityMode)
                    {
                        title = strings.ChannelVideoQualityUpdated(channel.Guild.Id);
                        description = strings.ChannelVideoQualityChange(channel.Guild.Id, channelIdStr,
                            voiceChannel.VideoQualityMode,
                            voiceChannel2.VideoQualityMode, updatedByStr);
                        changed = true;
                    }

                    break;
                }
            }

            if (changed && !string.IsNullOrEmpty(title))
            {
                var components = new ComponentBuilderV2()
                    .WithContainer([
                        new TextDisplayBuilder($"# {title}")
                    ], Mewdeko.OkColor)
                    .WithSeparator()
                    .WithContainer(new TextDisplayBuilder($"**Channel Update Details**\n{description}"));

                await logChannel.SendMessageAsync(components: components.Build(),
                    flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
                collector.Feature("logging", logChannel.Guild.Id);
            }
        }
    }

    /// <summary>
    ///     Handles the event when voice state is updated.
    /// </summary>
    /// <param name="user">The user that had their voice state updated.</param>
    /// <param name="oldState">The voice state before the update.</param>
    /// <param name="newState">The voice state after the update.</param>
    private async Task OnVoicePresence(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
    {
        if (user is not IGuildUser guildUser || IgnoresBot(guildUser.Guild.Id, user))
            return;

        // Check if old or new channel is ignored
        if ((oldState.VoiceChannel != null && IsChannelIgnored(guildUser.Guild.Id, oldState.VoiceChannel.Id)) ||
            (newState.VoiceChannel != null && IsChannelIgnored(guildUser.Guild.Id, newState.VoiceChannel.Id)))
            return;

        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.LogVoicePresenceId is null or 0)
                return;

            var logChannel = await guildUser.Guild.GetTextChannelAsync(logSetting.LogVoicePresenceId.Value);
            if (logChannel is null)
                return;

            var stateChanged = false;
            var userInfo = strings.UserInfoLine(guildUser.Guild.Id, user.Mention, user.Id).TrimEnd('\n');
            var title = "";
            var description = "";

            // Channel Changes
            if (oldState.VoiceChannel?.Id != newState.VoiceChannel?.Id)
            {
                if (oldState.VoiceChannel != null && newState.VoiceChannel != null)
                {
                    title = strings.UserMovedVoiceChannels(guildUser.Guild.Id);
                    description = strings.UserMovedVoiceChannelsDesc(guildUser.Guild.Id, userInfo,
                        oldState.VoiceChannel.Name,
                        newState.VoiceChannel.Name);
                }
                else if (oldState.VoiceChannel == null && newState.VoiceChannel != null)
                {
                    title = strings.UserJoinedVoiceChannel(guildUser.Guild.Id);
                    description = strings.UserJoinedVoiceChannelDesc(guildUser.Guild.Id, userInfo,
                        newState.VoiceChannel.Name);
                }
                else if (oldState.VoiceChannel != null && newState.VoiceChannel == null)
                {
                    title = strings.UserLeftVoiceChannel(guildUser.Guild.Id);
                    description = strings.UserLeftVoiceChannelDesc(guildUser.Guild.Id, userInfo,
                        oldState.VoiceChannel.Name);
                }

                stateChanged = true;
            }
            // Mute/Deafen Status Changes (only log if channel didn't change, to avoid spam)
            else if (newState.VoiceChannel != null) // Only log these if user is still in a channel
            {
                if (oldState.IsDeafened != newState.IsDeafened)
                {
                    title = newState.IsDeafened
                        ? strings.UserVoiceDeafened(guildUser.Guild.Id,
                            !newState.IsSelfDeafened
                                ? strings.Server(guildUser.Guild.Id)
                                : strings.Self(guildUser.Guild.Id))
                        : strings.UserVoiceUndeafened(guildUser.Guild.Id);
                    description = strings.UserVoiceStateDesc(guildUser.Guild.Id, userInfo,
                        newState.VoiceChannel.Name);
                    stateChanged = true;
                }
                else if (oldState.IsMuted != newState.IsMuted)
                {
                    title = newState.IsMuted
                        ? strings.UserVoiceMuted(guildUser.Guild.Id,
                            !newState.IsSelfMuted
                                ? strings.Server(guildUser.Guild.Id)
                                : strings.Self(guildUser.Guild.Id))
                        : strings.UserVoiceUnmuted(guildUser.Guild.Id, user.Username);
                    description = strings.UserVoiceStateDesc(guildUser.Guild.Id, userInfo,
                        newState.VoiceChannel.Name);
                    stateChanged = true;
                }
                else if (oldState.IsStreaming != newState.IsStreaming)
                {
                    title = newState.IsStreaming
                        ? strings.UserStartedStreaming(guildUser.Guild.Id)
                        : strings.UserStoppedStreaming(guildUser.Guild.Id);
                    description = strings.UserVoiceStateDesc(guildUser.Guild.Id, userInfo,
                        newState.VoiceChannel.Name);
                    stateChanged = true;
                }
                else if (oldState.IsVideoing != newState.IsVideoing)
                {
                    title = newState.IsVideoing
                        ? strings.UserStartedVideo(guildUser.Guild.Id)
                        : strings.UserStoppedVideo(guildUser.Guild.Id);
                    description = strings.UserVoiceStateDesc(guildUser.Guild.Id, userInfo,
                        newState.VoiceChannel.Name);
                    stateChanged = true;
                }
            }

            if (stateChanged && !string.IsNullOrEmpty(title))
            {
                var components = new ComponentBuilderV2()
                    .WithContainer([
                        new TextDisplayBuilder($"# {title}")
                    ], Mewdeko.OkColor)
                    .WithSeparator()
                    .WithContainer(new TextDisplayBuilder($"**Voice State Change**\n{description}"));

                await logChannel.SendMessageAsync(components: components.Build(),
                    flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
                collector.Feature("logging", logChannel.Guild.Id);
            }
        }
    }

    /// <summary>
    ///     Handles the event when a user says something using tts in a channel.
    /// </summary>
    /// <param name="user">The user that used tts.</param>
    /// <param name="oldState">The voice state before the update.</param>
    /// <param name="newState">The voice state after the update.</param>
    private async Task OnVoicePresenceTts(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
    {
        if (user is not IGuildUser guildUser || IgnoresBot(guildUser.Guild.Id, user))
            return;

        // Check if old or new channel is ignored
        if ((oldState.VoiceChannel != null && IsChannelIgnored(guildUser.Guild.Id, oldState.VoiceChannel.Id)) ||
            (newState.VoiceChannel != null && IsChannelIgnored(guildUser.Guild.Id, newState.VoiceChannel.Id)))
            return;

        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.LogVoicePresenceTtsId is null or 0)
                return;

            var logChannel = await guildUser.Guild.GetTextChannelAsync(logSetting.LogVoicePresenceTtsId.Value);
            if (logChannel is null)
                return;
            await Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Handles the event when a user is muted in a guild.
    /// </summary>
    /// <param name="guildUser">The user that was muted.</param>
    /// <param name="muter">The user that muted the user.</param>
    /// <param name="muteType">Type of mute. <see cref="MuteType" /></param>
    /// <param name="reason">The reason the user was muted.</param>
    private async Task OnUserMuted(IGuildUser guildUser, IUser muter, MuteType muteType, string reason)
    {
        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.UserMutedId is null or 0)
                return;

            var logChannel = await guildUser.Guild.GetTextChannelAsync(logSetting.UserMutedId.Value);
            if (logChannel is null)
                return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.UserMutedTitle(guildUser.GuildId, muteType)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithSection([
                    new TextDisplayBuilder(
                        $"**User Details**\n{strings.UserMutedDesc(guildUser.GuildId, guildUser.Mention, guildUser.Id)}")
                ], new ThumbnailBuilder(guildUser.RealAvatarUrl().ToString()))
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder($"**Moderation Information**\n" +
                                                      $"{strings.MutedByDesc(guildUser.GuildId, muter.Mention, muter.Id)}" +
                                                      $"{strings.MuteReasonDesc(guildUser.GuildId, reason ?? "N/A")}"));

            await logChannel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
            collector.Feature("logging", logChannel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a user is unmuted in a guild.
    /// </summary>
    /// <param name="guildUser">The user that was unmuted.</param>
    /// <param name="unmuter">The user that unmuted the user.</param>
    /// <param name="muteType">Type of mute. <see cref="MuteType" /></param>
    /// <param name="reason">The reason the user was unmuted.</param>
    private async Task OnUserUnmuted(IGuildUser guildUser, IUser unmuter, MuteType muteType, string reason)
    {
        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.UserMutedId is null or 0)
                return;

            var logChannel = await guildUser.Guild.GetTextChannelAsync(logSetting.UserMutedId.Value);
            if (logChannel is null)
                return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.UserUnmutedTitle(guildUser.GuildId, muteType)}")
                ], Mewdeko.OkColor)
                .WithSeparator()
                .WithSection([
                    new TextDisplayBuilder(
                        $"**User Details**\n{strings.UserUnmutedDesc(guildUser.GuildId, guildUser.Mention, guildUser.Id)}")
                ], new ThumbnailBuilder(guildUser.RealAvatarUrl().ToString()))
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder($"**Moderation Information**\n" +
                                                      $"{strings.UnmutedByDesc(guildUser.GuildId, unmuter.Mention, unmuter.Id)}" +
                                                      $"{strings.MuteReasonDesc(guildUser.GuildId, reason ?? "N/A")}"));

            await logChannel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
            collector.Feature("logging", logChannel.Guild.Id);
        }
    }

    /// <summary>
    ///     Sets the log channel for a specific type of log.
    /// </summary>
    /// <param name="guildId">The guildId to set the log setting for.</param>
    /// <param name="channelId">The channelId to set the log channel to.</param>
    /// <param name="type">The type of log to set the channel for.</param>
    public async Task SetLogChannel(ulong guildId, ulong channelId, LogType type)
    {
        LoggingV2 logSetting;
        var isNew = false;
        await using var db = await dbFactory.CreateConnectionAsync();

        logSetting = await db.LoggingV2.FirstOrDefaultAsync(l => l.GuildId == guildId).ConfigureAwait(false);

        if (logSetting == null)
        {
            isNew = true;
            logSetting = new LoggingV2
            {
                GuildId = guildId
            };
        }

        SetLogChannelId(logSetting, type, channelId);

        try
        {
            if (isNew)
                await db.InsertAsync(logSetting).ConfigureAwait(false);
            else
                await db.UpdateAsync(logSetting).ConfigureAwait(false);

            GuildLogSettings.AddOrUpdate(guildId, logSetting, (_, _) => logSetting);
        }
        catch (Exception e)
        {
            logger.LogError(e, "There was an issue setting log settings for Guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Allows you to set the log channel for a specific category of logs.
    /// </summary>
    /// <param name="guildId">The guildId to set the logs for.</param>
    /// <param name="channelId">The channelId to set the logs to.</param>
    /// <param name="categoryTypes">The category of logs to set the channel for.</param>
    public async Task LogSetByType(ulong guildId, ulong channelId, LogCategoryTypes categoryTypes)
    {
        LoggingV2 logSetting;
        var isNew = false;
        await using var db = await dbFactory.CreateConnectionAsync();

        logSetting = await db.LoggingV2.FirstOrDefaultAsync(l => l.GuildId == guildId).ConfigureAwait(false);

        if (logSetting == null)
        {
            isNew = true;
            logSetting = new LoggingV2
            {
                GuildId = guildId
            };
        }

        switch (categoryTypes)
        {
            case LogCategoryTypes.All:
                logSetting.AvatarUpdatedId = channelId;
                logSetting.ChannelCreatedId = channelId;
                logSetting.ChannelDestroyedId = channelId;
                logSetting.ChannelUpdatedId = channelId;
                logSetting.EventCreatedId = channelId;
                logSetting.LogOtherId = channelId;
                logSetting.MessageDeletedId = channelId;
                logSetting.MessageUpdatedId = channelId;
                logSetting.NicknameUpdatedId = channelId;
                logSetting.RoleCreatedId = channelId;
                logSetting.RoleDeletedId = channelId;
                logSetting.RoleUpdatedId = channelId;
                logSetting.ServerUpdatedId = channelId;
                logSetting.ThreadCreatedId = channelId;
                logSetting.ThreadDeletedId = channelId;
                logSetting.ThreadUpdatedId = channelId;
                logSetting.UserBannedId = channelId;
                logSetting.UserJoinedId = channelId;
                logSetting.UserLeftId = channelId;
                logSetting.UserMutedId = channelId;
                logSetting.UsernameUpdatedId = channelId;
                logSetting.UserUnbannedId = channelId;
                logSetting.UserUpdatedId = channelId;
                logSetting.LogUserPresenceId = channelId;
                logSetting.LogVoicePresenceId = channelId;
                logSetting.UserRoleAddedId = channelId;
                logSetting.UserRoleRemovedId = channelId;
                logSetting.LogVoicePresenceTtsId = channelId;
                logSetting.InviteCreatedId = channelId;
                logSetting.InviteDeletedId = channelId;
                logSetting.MessagesBulkDeletedId = channelId;
                logSetting.ReactionEventsId = channelId;
                logSetting.EmojiCreatedId = channelId;
                logSetting.EmojiUpdatedId = channelId;
                logSetting.EmojiDeletedId = channelId;
                logSetting.StickerCreatedId = channelId;
                logSetting.StickerUpdatedId = channelId;
                logSetting.StickerDeletedId = channelId;
                logSetting.UserTimedOutId = channelId;
                logSetting.UserTimeoutRemovedId = channelId;
                logSetting.InviteLinkPostedId = channelId;
                logSetting.ChannelPermissionsUpdatedId = channelId;
                logSetting.ServerBoostedId = channelId;
                logSetting.IntegrationChangedId = channelId;
                logSetting.WebhookChangedId = channelId;
                logSetting.StageChangedId = channelId;
                logSetting.ScheduledEventChangedId = channelId;
                logSetting.AutoModRuleChangedId = channelId;
                logSetting.SoundboardChangedId = channelId;
                logSetting.ThreadMemberChangedId = channelId;
                break;
            case LogCategoryTypes.Users:
                logSetting.NicknameUpdatedId = channelId;
                logSetting.AvatarUpdatedId = channelId;
                logSetting.UsernameUpdatedId = channelId;
                logSetting.UserRoleAddedId = channelId;
                logSetting.UserRoleRemovedId = channelId;
                logSetting.LogVoicePresenceId = channelId;
                logSetting.UserJoinedId = channelId;
                logSetting.UserLeftId = channelId;
                logSetting.UserUpdatedId = channelId;
                break;
            case LogCategoryTypes.Threads:
                logSetting.ThreadCreatedId = channelId;
                logSetting.ThreadDeletedId = channelId;
                logSetting.ThreadUpdatedId = channelId;
                logSetting.ThreadMemberChangedId = channelId;
                break;
            case LogCategoryTypes.Roles:
                logSetting.RoleCreatedId = channelId;
                logSetting.RoleDeletedId = channelId;
                logSetting.RoleUpdatedId = channelId;
                break;
            case LogCategoryTypes.Server:
                logSetting.ServerUpdatedId = channelId;
                logSetting.EventCreatedId = channelId;
                logSetting.InviteCreatedId = channelId;
                logSetting.InviteDeletedId = channelId;
                logSetting.EmojiCreatedId = channelId;
                logSetting.EmojiUpdatedId = channelId;
                logSetting.EmojiDeletedId = channelId;
                logSetting.StickerCreatedId = channelId;
                logSetting.StickerUpdatedId = channelId;
                logSetting.StickerDeletedId = channelId;
                logSetting.ServerBoostedId = channelId;
                logSetting.IntegrationChangedId = channelId;
                logSetting.WebhookChangedId = channelId;
                logSetting.StageChangedId = channelId;
                logSetting.ScheduledEventChangedId = channelId;
                logSetting.SoundboardChangedId = channelId;
                break;
            case LogCategoryTypes.Channel:
                logSetting.ChannelUpdatedId = channelId;
                logSetting.ChannelCreatedId = channelId;
                logSetting.ChannelDestroyedId = channelId;
                logSetting.ChannelPermissionsUpdatedId = channelId;
                break;
            case LogCategoryTypes.Messages:
                logSetting.MessageDeletedId = channelId;
                logSetting.MessageUpdatedId = channelId;
                logSetting.MessagesBulkDeletedId = channelId;
                logSetting.ReactionEventsId = channelId;
                logSetting.InviteLinkPostedId = channelId;
                break;
            case LogCategoryTypes.Moderation:
                logSetting.UserTimedOutId = channelId;
                logSetting.UserTimeoutRemovedId = channelId;
                logSetting.UserMutedId = channelId;
                logSetting.UserBannedId = channelId;
                logSetting.UserUnbannedId = channelId;
                logSetting.LogOtherId = channelId;
                logSetting.AutoModRuleChangedId = channelId;
                break;
            case LogCategoryTypes.None:
                ulong? noneValue = null;
                logSetting.AvatarUpdatedId = noneValue;
                logSetting.ChannelCreatedId = noneValue;
                logSetting.ChannelDestroyedId = noneValue;
                logSetting.ChannelUpdatedId = noneValue;
                logSetting.EventCreatedId = noneValue;
                logSetting.LogOtherId = noneValue;
                logSetting.MessageDeletedId = noneValue;
                logSetting.MessageUpdatedId = noneValue;
                logSetting.NicknameUpdatedId = noneValue;
                logSetting.RoleCreatedId = noneValue;
                logSetting.RoleDeletedId = noneValue;
                logSetting.RoleUpdatedId = noneValue;
                logSetting.ServerUpdatedId = noneValue;
                logSetting.ThreadCreatedId = noneValue;
                logSetting.ThreadDeletedId = noneValue;
                logSetting.ThreadUpdatedId = noneValue;
                logSetting.UserBannedId = noneValue;
                logSetting.UserJoinedId = noneValue;
                logSetting.UserLeftId = noneValue;
                logSetting.UserMutedId = noneValue;
                logSetting.UsernameUpdatedId = noneValue;
                logSetting.UserUnbannedId = noneValue;
                logSetting.UserUpdatedId = noneValue;
                logSetting.LogUserPresenceId = noneValue;
                logSetting.LogVoicePresenceId = noneValue;
                logSetting.UserRoleAddedId = noneValue;
                logSetting.UserRoleRemovedId = noneValue;
                logSetting.LogVoicePresenceTtsId = noneValue;
                logSetting.InviteCreatedId = noneValue;
                logSetting.InviteDeletedId = noneValue;
                logSetting.MessagesBulkDeletedId = noneValue;
                logSetting.ReactionEventsId = noneValue;
                logSetting.EmojiCreatedId = noneValue;
                logSetting.EmojiUpdatedId = noneValue;
                logSetting.EmojiDeletedId = noneValue;
                logSetting.StickerCreatedId = noneValue;
                logSetting.StickerUpdatedId = noneValue;
                logSetting.StickerDeletedId = noneValue;
                logSetting.UserTimedOutId = noneValue;
                logSetting.UserTimeoutRemovedId = noneValue;
                logSetting.InviteLinkPostedId = noneValue;
                logSetting.ChannelPermissionsUpdatedId = noneValue;
                logSetting.ServerBoostedId = noneValue;
                logSetting.IntegrationChangedId = noneValue;
                logSetting.WebhookChangedId = noneValue;
                logSetting.StageChangedId = noneValue;
                logSetting.ScheduledEventChangedId = noneValue;
                logSetting.AutoModRuleChangedId = noneValue;
                logSetting.SoundboardChangedId = noneValue;
                logSetting.ThreadMemberChangedId = noneValue;
                break;
        }

        try
        {
            if (isNew)
                await db.InsertAsync(logSetting).ConfigureAwait(false);
            else
                await db.UpdateAsync(logSetting).ConfigureAwait(false);

            GuildLogSettings.AddOrUpdate(guildId, logSetting, (_, _) => logSetting);
        }
        catch (Exception e)
        {
            logger.LogError(e, "There was an issue setting log settings by type for Guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Log types grouped by the channel the one-command setup routes them to.
    /// </summary>
    private static readonly (string Name, LogType[] Types)[] SetupGroups =
    [
        ("message-logs",
            [
                LogType.MessageUpdated, LogType.MessageDeleted, LogType.MessagesBulkDeleted, LogType.ReactionEvents,
                LogType.InviteLinkPosted
            ]),
        ("member-logs",
            [
                LogType.UserJoined, LogType.UserLeft, LogType.UsernameUpdated, LogType.NicknameUpdated,
                LogType.AvatarUpdated, LogType.UserUpdated, LogType.UserRoleAdded, LogType.UserRoleRemoved
            ]),
        ("moderation-logs",
            [
                LogType.UserBanned, LogType.UserUnbanned, LogType.UserMuted, LogType.UserTimedOut,
                LogType.UserTimeoutRemoved, LogType.AutoModRuleChanged, LogType.Other
            ]),
        ("server-logs",
            [
                LogType.ServerUpdated, LogType.EventCreated, LogType.InviteCreated, LogType.InviteDeleted,
                LogType.RoleCreated, LogType.RoleUpdated, LogType.RoleDeleted, LogType.ChannelCreated,
                LogType.ChannelUpdated, LogType.ChannelDestroyed, LogType.ThreadCreated, LogType.ThreadUpdated,
                LogType.ThreadDeleted, LogType.EmojiCreated, LogType.EmojiUpdated, LogType.EmojiDeleted,
                LogType.StickerCreated, LogType.StickerUpdated, LogType.StickerDeleted,
                LogType.ChannelPermissionsUpdated, LogType.ThreadMemberChanged, LogType.ServerBoosted,
                LogType.IntegrationChanged, LogType.WebhookChanged, LogType.StageChanged,
                LogType.ScheduledEventChanged, LogType.SoundboardChanged
            ]),
        ("voice-logs", [LogType.VoicePresence, LogType.VoicePresenceTts])
    ];

    /// <summary>
    ///     Gets the channel configured for a log type.
    /// </summary>
    /// <param name="l">The log settings.</param>
    /// <param name="type">The log type.</param>
    /// <returns>The channel id, or null if none is set.</returns>
    public static ulong? GetLogChannelId(LoggingV2 l, LogType type)
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
            LogType.EmojiCreated => l.EmojiCreatedId,
            LogType.EmojiUpdated => l.EmojiUpdatedId,
            LogType.EmojiDeleted => l.EmojiDeletedId,
            LogType.StickerCreated => l.StickerCreatedId,
            LogType.StickerUpdated => l.StickerUpdatedId,
            LogType.StickerDeleted => l.StickerDeletedId,
            LogType.UserTimedOut => l.UserTimedOutId,
            LogType.UserTimeoutRemoved => l.UserTimeoutRemovedId,
            LogType.InviteLinkPosted => l.InviteLinkPostedId,
            LogType.ChannelPermissionsUpdated => l.ChannelPermissionsUpdatedId,
            LogType.ServerBoosted => l.ServerBoostedId,
            LogType.IntegrationChanged => l.IntegrationChangedId,
            LogType.WebhookChanged => l.WebhookChangedId,
            LogType.StageChanged => l.StageChangedId,
            LogType.ScheduledEventChanged => l.ScheduledEventChangedId,
            LogType.AutoModRuleChanged => l.AutoModRuleChangedId,
            LogType.SoundboardChanged => l.SoundboardChangedId,
            LogType.ThreadMemberChanged => l.ThreadMemberChangedId,
            _ => null
        };
    }

    private static void SetLogChannelId(LoggingV2 l, LogType type, ulong? channelId)
    {
        switch (type)
        {
            case LogType.Other: l.LogOtherId = channelId; break;
            case LogType.EventCreated: l.EventCreatedId = channelId; break;
            case LogType.RoleUpdated: l.RoleUpdatedId = channelId; break;
            case LogType.RoleCreated: l.RoleCreatedId = channelId; break;
            case LogType.ServerUpdated: l.ServerUpdatedId = channelId; break;
            case LogType.ThreadCreated: l.ThreadCreatedId = channelId; break;
            case LogType.UserRoleAdded: l.UserRoleAddedId = channelId; break;
            case LogType.UserRoleRemoved: l.UserRoleRemovedId = channelId; break;
            case LogType.UsernameUpdated: l.UsernameUpdatedId = channelId; break;
            case LogType.NicknameUpdated: l.NicknameUpdatedId = channelId; break;
            case LogType.AvatarUpdated: l.AvatarUpdatedId = channelId; break;
            case LogType.ThreadDeleted: l.ThreadDeletedId = channelId; break;
            case LogType.ThreadUpdated: l.ThreadUpdatedId = channelId; break;
            case LogType.MessageUpdated: l.MessageUpdatedId = channelId; break;
            case LogType.MessageDeleted: l.MessageDeletedId = channelId; break;
            case LogType.UserJoined: l.UserJoinedId = channelId; break;
            case LogType.UserLeft: l.UserLeftId = channelId; break;
            case LogType.UserBanned: l.UserBannedId = channelId; break;
            case LogType.UserUnbanned: l.UserUnbannedId = channelId; break;
            case LogType.UserUpdated: l.UserUpdatedId = channelId; break;
            case LogType.ChannelCreated: l.ChannelCreatedId = channelId; break;
            case LogType.ChannelDestroyed: l.ChannelDestroyedId = channelId; break;
            case LogType.ChannelUpdated: l.ChannelUpdatedId = channelId; break;
            case LogType.VoicePresence: l.LogVoicePresenceId = channelId; break;
            case LogType.VoicePresenceTts: l.LogVoicePresenceTtsId = channelId; break;
            case LogType.UserMuted: l.UserMutedId = channelId; break;
            case LogType.RoleDeleted: l.RoleDeletedId = channelId; break;
            case LogType.InviteCreated: l.InviteCreatedId = channelId; break;
            case LogType.InviteDeleted: l.InviteDeletedId = channelId; break;
            case LogType.MessagesBulkDeleted: l.MessagesBulkDeletedId = channelId; break;
            case LogType.ReactionEvents: l.ReactionEventsId = channelId; break;
            case LogType.EmojiCreated: l.EmojiCreatedId = channelId; break;
            case LogType.EmojiUpdated: l.EmojiUpdatedId = channelId; break;
            case LogType.EmojiDeleted: l.EmojiDeletedId = channelId; break;
            case LogType.StickerCreated: l.StickerCreatedId = channelId; break;
            case LogType.StickerUpdated: l.StickerUpdatedId = channelId; break;
            case LogType.StickerDeleted: l.StickerDeletedId = channelId; break;
            case LogType.UserTimedOut: l.UserTimedOutId = channelId; break;
            case LogType.UserTimeoutRemoved: l.UserTimeoutRemovedId = channelId; break;
            case LogType.InviteLinkPosted: l.InviteLinkPostedId = channelId; break;
            case LogType.ChannelPermissionsUpdated: l.ChannelPermissionsUpdatedId = channelId; break;
            case LogType.ServerBoosted: l.ServerBoostedId = channelId; break;
            case LogType.IntegrationChanged: l.IntegrationChangedId = channelId; break;
            case LogType.WebhookChanged: l.WebhookChangedId = channelId; break;
            case LogType.StageChanged: l.StageChangedId = channelId; break;
            case LogType.ScheduledEventChanged: l.ScheduledEventChangedId = channelId; break;
            case LogType.AutoModRuleChanged: l.AutoModRuleChangedId = channelId; break;
            case LogType.SoundboardChanged: l.SoundboardChangedId = channelId; break;
            case LogType.ThreadMemberChanged: l.ThreadMemberChangedId = channelId; break;
        }
    }

    private async Task SaveLogSettingsAsync(ulong guildId, Action<LoggingV2> mutate)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var logSetting = await db.LoggingV2.FirstOrDefaultAsync(l => l.GuildId == guildId).ConfigureAwait(false);
        var isNew = logSetting == null;
        logSetting ??= new LoggingV2
        {
            GuildId = guildId
        };

        mutate(logSetting);

        try
        {
            if (isNew)
                await db.InsertAsync(logSetting).ConfigureAwait(false);
            else
                await db.UpdateAsync(logSetting).ConfigureAwait(false);

            GuildLogSettings.AddOrUpdate(guildId, logSetting, (_, _) => logSetting);
        }
        catch (Exception e)
        {
            logger.LogError(e, "There was an issue saving log settings for Guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Creates a private "Logs" category with a channel per log group and routes every log type to its channel.
    /// </summary>
    /// <param name="guild">The guild to set logging up in.</param>
    /// <returns>The channels used, keyed by name, or null if they could not be created.</returns>
    public async Task<Dictionary<string, ITextChannel>?> SetupLogChannels(IGuild guild)
    {
        try
        {
            var overwrites = new List<Overwrite>
            {
                new(guild.EveryoneRole.Id, PermissionTarget.Role,
                    new OverwritePermissions(viewChannel: PermValue.Deny)),
                new(client.CurrentUser.Id, PermissionTarget.User,
                    new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow,
                        embedLinks: PermValue.Allow, attachFiles: PermValue.Allow))
            };

            var existing = await guild.GetChannelsAsync().ConfigureAwait(false);
            var category = existing.OfType<ICategoryChannel>()
                               .FirstOrDefault(c => c.Name.Equals("Logs", StringComparison.OrdinalIgnoreCase))
                           ?? await guild.CreateCategoryAsync("Logs", p => p.PermissionOverwrites = overwrites)
                               .ConfigureAwait(false);

            var channels = new Dictionary<string, ITextChannel>();
            var routes = new Dictionary<LogType, ulong>();
            foreach (var (name, types) in SetupGroups)
            {
                var channel = existing.OfType<ITextChannel>()
                                  .FirstOrDefault(c => c.CategoryId == category.Id &&
                                                       c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                              ?? await guild.CreateTextChannelAsync(name, p =>
                              {
                                  p.CategoryId = category.Id;
                                  p.PermissionOverwrites = overwrites;
                              }).ConfigureAwait(false);

                channels[name] = channel;
                foreach (var type in types)
                    routes[type] = channel.Id;
            }

            await ApplyLogChannels(guild.Id, routes).ConfigureAwait(false);
            return channels;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set up log channels for guild {GuildId}", guild.Id);
            return null;
        }
    }

    /// <summary>
    ///     Sets the channel for several log types in one save.
    /// </summary>
    /// <param name="guildId">The guild to update.</param>
    /// <param name="routes">The channel to use for each log type.</param>
    public Task ApplyLogChannels(ulong guildId, IReadOnlyDictionary<LogType, ulong> routes)
    {
        return SaveLogSettingsAsync(guildId, l =>
        {
            foreach (var (type, channelId) in routes)
                SetLogChannelId(l, type, channelId);
        });
    }

    private static List<string> ParsePrefixes(string? stored)
    {
        return string.IsNullOrEmpty(stored)
            ? []
            : stored.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    ///     Gets the prefixes whose messages are skipped by message edit and delete logs.
    /// </summary>
    /// <param name="guildId">The guild to get prefixes for.</param>
    /// <returns>The configured prefixes.</returns>
    public List<string> GetMessageIgnorePrefixes(ulong guildId)
    {
        return GuildLogSettings.TryGetValue(guildId, out var l) ? ParsePrefixes(l.MessageIgnorePrefixes) : [];
    }

    /// <summary>
    ///     Sets the prefixes whose messages are skipped by message edit and delete logs.
    /// </summary>
    /// <param name="guildId">The guild to update.</param>
    /// <param name="prefixes">The prefixes to skip. An empty collection clears them.</param>
    public Task SetMessageIgnorePrefixes(ulong guildId, IEnumerable<string> prefixes)
    {
        var cleaned = prefixes.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToList();
        return SaveLogSettingsAsync(guildId,
            l => l.MessageIgnorePrefixes = cleaned.Count == 0 ? null : string.Join('\n', cleaned));
    }

    /// <summary>
    ///     Gets whether a guild's logs skip actions by bots.
    /// </summary>
    /// <param name="guildId">The guild to check.</param>
    /// <returns>True when bots are skipped, which is the default.</returns>
    public bool GetIgnoreBots(ulong guildId)
    {
        return !GuildLogSettings.TryGetValue(guildId, out var l) || l.IgnoreBots;
    }

    /// <summary>
    ///     Sets whether a guild's logs skip actions by bots.
    /// </summary>
    /// <param name="guildId">The guild to update.</param>
    /// <param name="ignore">Whether to skip bots.</param>
    public Task SetIgnoreBots(ulong guildId, bool ignore)
    {
        return SaveLogSettingsAsync(guildId, l => l.IgnoreBots = ignore);
    }

    private bool IgnoresBot(ulong guildId, IUser? user)
    {
        return user?.IsBot == true && GetIgnoreBots(guildId);
    }

    /// <summary>
    ///     Logs a server lockdown starting or ending.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="started">True when the lockdown started, false when it ended.</param>
    /// <param name="lockdownType">The kind of lockdown.</param>
    /// <param name="moderator">Who started or lifted it.</param>
    public async Task LogLockdownAsync(IGuild guild, bool started, string lockdownType, IUser? moderator)
    {
        if (!GuildLogSettings.TryGetValue(guild.Id, out var logSetting) || logSetting.LogOtherId is null or 0 ||
            await guild.GetTextChannelAsync(logSetting.LogOtherId.Value).ConfigureAwait(false) is not { } channel)
            return;

        var gid = guild.Id;
        var details = strings.LogLockdownTypeField(gid, lockdownType);
        if (moderator is not null)
            details += strings.LogByField(gid, moderator.Mention, moderator.Id);

        try
        {
            await SendLogAsync(channel, new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder(
                        $"# {(started ? strings.LogLockdownStarted(gid) : strings.LogLockdownEnded(gid))}"),
                    new TextDisplayBuilder(details)
                ], started ? Mewdeko.ErrorColor : Mewdeko.OkColor)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not log a lockdown in {GuildId}", gid);
        }
    }

    private static bool StartsWithIgnoredPrefix(LoggingV2 l, string? content)
    {
        if (string.IsNullOrEmpty(l.MessageIgnorePrefixes) || string.IsNullOrEmpty(content))
            return false;

        return ParsePrefixes(l.MessageIgnorePrefixes)
            .Any(p => content.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Exports a guild's log configuration.
    /// </summary>
    /// <param name="guild">The guild to export.</param>
    /// <returns>The configuration as indented JSON and as a short code, or null if nothing is configured.</returns>
    public async Task<(string Json, string Code)?> ExportConfig(IGuild guild)
    {
        if (!GuildLogSettings.TryGetValue(guild.Id, out var l))
            return null;

        var channels = (await guild.GetTextChannelsAsync().ConfigureAwait(false)).ToDictionary(c => c.Id);
        var export = new LogConfigExport
        {
            Prefixes = ParsePrefixes(l.MessageIgnorePrefixes)
        };

        foreach (var type in Enum.GetValues<LogType>())
        {
            var id = GetLogChannelId(l, type);
            if (id is null or 0 || !channels.TryGetValue(id.Value, out var channel))
                continue;

            export.Types[type.ToString()] = new ExportedChannel
            {
                Id = channel.Id, Name = channel.Name
            };
        }

        foreach (var id in await GetIgnoredChannels(guild.Id).ConfigureAwait(false))
        {
            if (channels.TryGetValue(id, out var channel))
                export.Ignored.Add(new ExportedChannel
                {
                    Id = channel.Id, Name = channel.Name
                });
        }

        if (export.Types.Count == 0 && export.Ignored.Count == 0 && export.Prefixes.Count == 0)
            return null;

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        using var output = new MemoryStream();
        await using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, true))
        {
            await gzip.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(export)).ConfigureAwait(false);
        }

        return (json, Convert.ToBase64String(output.ToArray()));
    }

    /// <summary>
    ///     Imports a log configuration from a code or JSON, mapping channels by id and then by name.
    /// </summary>
    /// <param name="guild">The guild to import into.</param>
    /// <param name="input">The export code or JSON.</param>
    /// <returns>What was applied and which channels could not be mapped.</returns>
    public async Task<LogImportResult> ImportConfig(IGuild guild, string input)
    {
        LogConfigExport? export;
        try
        {
            input = input.Trim().Trim('`');
            if (input.StartsWith('{'))
            {
                export = JsonSerializer.Deserialize<LogConfigExport>(input);
            }
            else
            {
                await using var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(input)),
                    CompressionMode.Decompress);
                export = await JsonSerializer.DeserializeAsync<LogConfigExport>(gzip).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            return new LogImportResult(0, [], false);
        }

        if (export is null)
            return new LogImportResult(0, [], false);

        var channels = (await guild.GetTextChannelsAsync().ConfigureAwait(false)).ToList();
        var unmapped = new HashSet<string>();

        ITextChannel? Resolve(ExportedChannel exported)
        {
            var found = channels.FirstOrDefault(c => c.Id == exported.Id)
                        ?? channels.FirstOrDefault(c => c.Name.Equals(exported.Name, StringComparison.OrdinalIgnoreCase));
            if (found is null)
                unmapped.Add($"#{exported.Name}");
            return found;
        }

        var routes = new Dictionary<LogType, ulong>();
        foreach (var (typeName, exported) in export.Types)
        {
            if (!Enum.TryParse<LogType>(typeName, true, out var type))
                continue;

            var channel = Resolve(exported);
            if (channel is not null)
                routes[type] = channel.Id;
        }

        var ignored = export.Ignored.Select(Resolve).OfType<ITextChannel>().Select(c => c.Id).ToHashSet();

        await SaveLogSettingsAsync(guild.Id, l =>
        {
            foreach (var (type, channelId) in routes)
                SetLogChannelId(l, type, channelId);

            var prefixes = export.Prefixes.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToList();
            l.MessageIgnorePrefixes = prefixes.Count == 0 ? null : string.Join('\n', prefixes);
        }).ConfigureAwait(false);

        await UpdateIgnoredChannelsAsync(guild.Id, ignored).ConfigureAwait(false);
        return new LogImportResult(routes.Count, unmapped.ToList(), true);
    }

    /// <summary>
    ///     Downloads a small text file, such as an exported log configuration.
    /// </summary>
    /// <param name="url">The attachment url.</param>
    /// <returns>The file text, or null if it could not be read.</returns>
    public async Task<string?> DownloadText(string url)
    {
        try
        {
            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            var text = await http.GetStringAsync(url).ConfigureAwait(false);
            return text.Length > 200_000 ? null : text;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not download log configuration file");
            return null;
        }
    }

    private async Task SendLogAsync(ITextChannel channel, ComponentBuilderV2 components)
    {
        await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
            allowedMentions: AllowedMentions.None);
        collector.Feature("logging", channel.Guild.Id);
    }

    private static string EmojiRoleIds(GuildEmote emote)
    {
        return string.Join(",", emote.RoleIds.OrderBy(x => x));
    }

    /// <summary>
    ///     Handles custom emoji being created, renamed or deleted, detected from guild updates.
    /// </summary>
    /// <param name="before">The guild before the update.</param>
    /// <param name="after">The guild after the update.</param>
    private async Task OnEmojisUpdated(SocketGuild before, SocketGuild after)
    {
        if (!GuildLogSettings.TryGetValue(after.Id, out var logSetting))
            return;

        var oldEmotes = before.Emotes.ToDictionary(e => e.Id);
        var newEmotes = after.Emotes.ToDictionary(e => e.Id);
        if (oldEmotes.Count == newEmotes.Count && oldEmotes.Keys.All(newEmotes.ContainsKey) &&
            oldEmotes.All(o => o.Value.Name == newEmotes[o.Key].Name &&
                               EmojiRoleIds(o.Value) == EmojiRoleIds(newEmotes[o.Key])))
            return;

        var gid = after.Id;
        foreach (var emote in newEmotes.Values.Where(e => !oldEmotes.ContainsKey(e.Id)))
        {
            if (logSetting.EmojiCreatedId is null or 0 || after.GetTextChannel(logSetting.EmojiCreatedId.Value) is not
                { } channel)
                break;

            var by = (await GetAuditLogsSafeAsync(after, ActionType.EmojiCreated)).FirstOrDefault()?.User;
            await SendLogAsync(channel, new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.LogEmojiCreated(gid)}"),
                    new TextDisplayBuilder(strings.NameField(gid, emote.Name) + strings.IdField(gid, emote.Id) +
                                           (by is null ? "" : strings.CreatedByField(gid, by.Mention, by.Id)))
                ], Mewdeko.OkColor)
                .WithMediaGallery([
                    new MediaGalleryItemProperties
                    {
                        Media = new UnfurledMediaItemProperties(emote.Url)
                    }
                ]));
        }

        foreach (var emote in oldEmotes.Values.Where(e => !newEmotes.ContainsKey(e.Id)))
        {
            if (logSetting.EmojiDeletedId is null or 0 || after.GetTextChannel(logSetting.EmojiDeletedId.Value) is not
                { } channel)
                break;

            var by = (await GetAuditLogsSafeAsync(after, ActionType.EmojiDeleted)).FirstOrDefault()?.User;
            await SendLogAsync(channel, new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.LogEmojiDeleted(gid)}"),
                    new TextDisplayBuilder(strings.NameField(gid, emote.Name) + strings.IdField(gid, emote.Id) +
                                           (by is null ? "" : strings.DeletedByField(gid, by.Mention, by.Id)))
                ], Mewdeko.ErrorColor));
        }

        foreach (var emote in newEmotes.Values.Where(e => oldEmotes.TryGetValue(e.Id, out var old) &&
                                                          (old.Name != e.Name ||
                                                           EmojiRoleIds(old) != EmojiRoleIds(e))))
        {
            if (logSetting.EmojiUpdatedId is null or 0 || after.GetTextChannel(logSetting.EmojiUpdatedId.Value) is not
                { } channel)
                break;

            var old = oldEmotes[emote.Id];
            var by = (await GetAuditLogsSafeAsync(after, ActionType.EmojiUpdated)).FirstOrDefault()?.User;
            await SendLogAsync(channel, new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.LogEmojiUpdated(gid)}"),
                    new TextDisplayBuilder(strings.LogNameChangeField(gid, old.Name, emote.Name) +
                                           strings.IdField(gid, emote.Id) +
                                           (by is null ? "" : strings.UpdatedByField(gid, by.Mention, by.Id)))
                ], Mewdeko.OkColor)
                .WithMediaGallery([
                    new MediaGalleryItemProperties
                    {
                        Media = new UnfurledMediaItemProperties(emote.Url)
                    }
                ]));
        }
    }

    /// <summary>
    ///     Handles a custom sticker being created.
    /// </summary>
    /// <param name="sticker">The created sticker.</param>
    private async Task OnStickerCreated(SocketCustomSticker sticker)
    {
        if (!GuildLogSettings.TryGetValue(sticker.Guild.Id, out var logSetting) ||
            logSetting.StickerCreatedId is null or 0 ||
            sticker.Guild.GetTextChannel(logSetting.StickerCreatedId.Value) is not { } channel)
            return;

        var gid = sticker.Guild.Id;
        var by = sticker.Author;
        await SendLogAsync(channel, new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {strings.LogStickerCreated(gid)}"),
                new TextDisplayBuilder(strings.NameField(gid, sticker.Name) + strings.IdField(gid, sticker.Id) +
                                       (by is null ? "" : strings.CreatedByField(gid, by.Mention, by.Id)))
            ], Mewdeko.OkColor)
            .WithMediaGallery([
                new MediaGalleryItemProperties
                {
                    Media = new UnfurledMediaItemProperties(sticker.GetStickerUrl())
                }
            ]));
    }

    /// <summary>
    ///     Handles a custom sticker being edited.
    /// </summary>
    /// <param name="before">The sticker before the edit.</param>
    /// <param name="after">The sticker after the edit.</param>
    private async Task OnStickerUpdated(SocketCustomSticker before, SocketCustomSticker after)
    {
        if (before.Name == after.Name && before.Description == after.Description &&
            before.Tags.SequenceEqual(after.Tags))
            return;

        if (!GuildLogSettings.TryGetValue(after.Guild.Id, out var logSetting) ||
            logSetting.StickerUpdatedId is null or 0 ||
            after.Guild.GetTextChannel(logSetting.StickerUpdatedId.Value) is not { } channel)
            return;

        var gid = after.Guild.Id;
        var text = strings.LogNameChangeField(gid, before.Name, after.Name) + strings.IdField(gid, after.Id);
        if (before.Description != after.Description)
            text += $"`Description:` {before.Description ?? "N/A"} to {after.Description ?? "N/A"}\n";
        if (!before.Tags.SequenceEqual(after.Tags))
            text += $"`Tags:` {string.Join(", ", before.Tags)} to {string.Join(", ", after.Tags)}\n";

        await SendLogAsync(channel, new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {strings.LogStickerUpdated(gid)}"),
                new TextDisplayBuilder(text)
            ], Mewdeko.OkColor)
            .WithMediaGallery([
                new MediaGalleryItemProperties
                {
                    Media = new UnfurledMediaItemProperties(after.GetStickerUrl())
                }
            ]));
    }

    /// <summary>
    ///     Handles a custom sticker being deleted.
    /// </summary>
    /// <param name="sticker">The deleted sticker.</param>
    private async Task OnStickerDeleted(SocketCustomSticker sticker)
    {
        if (!GuildLogSettings.TryGetValue(sticker.Guild.Id, out var logSetting) ||
            logSetting.StickerDeletedId is null or 0 ||
            sticker.Guild.GetTextChannel(logSetting.StickerDeletedId.Value) is not { } channel)
            return;

        var gid = sticker.Guild.Id;
        var by = (await GetAuditLogsSafeAsync(sticker.Guild, ActionType.StickerDeleted)).FirstOrDefault()?.User;
        await SendLogAsync(channel, new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {strings.LogStickerDeleted(gid)}"),
                new TextDisplayBuilder(strings.NameField(gid, sticker.Name) + strings.IdField(gid, sticker.Id) +
                                       (by is null ? "" : strings.DeletedByField(gid, by.Mention, by.Id)))
            ], Mewdeko.ErrorColor));
    }

    /// <summary>
    ///     Handles a member being timed out or having a timeout removed.
    /// </summary>
    /// <param name="cacheable">The member before the update.</param>
    /// <param name="after">The member after the update.</param>
    private async Task OnUserTimeoutChanged(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser after)
    {
        if (!GuildLogSettings.TryGetValue(after.Guild.Id, out var logSetting))
            return;

        var now = DateTimeOffset.UtcNow;
        var oldUntil = cacheable.HasValue && cacheable.Value.TimedOutUntil is { } o && o > now
            ? o
            : (DateTimeOffset?)null;
        var newUntil = after.TimedOutUntil is { } n && n > now ? n : (DateTimeOffset?)null;
        if (cacheable.HasValue && oldUntil == newUntil)
            return;

        if (newUntil is not null)
            await TrackTimeoutAsync(after.Guild.Id, after.Id, newUntil.Value, logSetting);
        else if (oldUntil is not null)
            await UntrackTimeoutAsync(after.Guild.Id, after.Id);

        if (!cacheable.HasValue)
            return;

        var gid = after.Guild.Id;
        if (newUntil is not null)
        {
            if (logSetting.UserTimedOutId is null or 0 ||
                after.Guild.GetTextChannel(logSetting.UserTimedOutId.Value) is not { } channel)
                return;

            await Task.Delay(500);
            var by = (await GetAuditLogsSafeAsync(after.Guild, ActionType.MemberUpdated)).FirstOrDefault()?.User;
            await SendLogAsync(channel, new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.LogUserTimedOut(gid)}"),
                    new TextDisplayBuilder(strings.UserField(gid, after.Mention, after.Id) +
                                           strings.LogTimeoutUntilField(gid, $"<t:{newUntil.Value.ToUnixTimeSeconds()}:f>") +
                                           (by is null ? "" : strings.UpdatedByField(gid, by.Mention, by.Id)))
                ], Mewdeko.ErrorColor));
        }
        else
        {
            if (logSetting.UserTimeoutRemovedId is null or 0 ||
                after.Guild.GetTextChannel(logSetting.UserTimeoutRemovedId.Value) is not { } channel)
                return;

            await Task.Delay(500);
            var by = (await GetAuditLogsSafeAsync(after.Guild, ActionType.MemberUpdated)).FirstOrDefault()?.User;
            await SendLogAsync(channel, new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.LogUserTimeoutRemoved(gid)}"),
                    new TextDisplayBuilder(strings.UserField(gid, after.Mention, after.Id) +
                                           (by is null ? "" : strings.UpdatedByField(gid, by.Mention, by.Id)))
                ], Mewdeko.OkColor));
        }
    }

    /// <summary>
    ///     Starts watching a timeout so its end can be logged, when the guild logs removed timeouts.
    /// </summary>
    /// <param name="guildId">The guild the member is timed out in.</param>
    /// <param name="userId">The timed out member.</param>
    /// <param name="until">When the timeout ends.</param>
    /// <param name="logSetting">The guild's log settings.</param>
    private async Task TrackTimeoutAsync(ulong guildId, ulong userId, DateTimeOffset until, LoggingV2 logSetting)
    {
        if (logSetting.UserTimeoutRemovedId is null or 0)
            return;

        try
        {
            var endsAt = until.UtcDateTime;
            await using var db = await dbFactory.CreateConnectionAsync();
            var updated = await db.TrackedTimeouts
                .Where(x => x.GuildId == guildId && x.UserId == userId)
                .Set(x => x.EndsAt, endsAt)
                .UpdateAsync();
            if (updated == 0)
                await db.InsertAsync(new TrackedTimeout
                {
                    GuildId = guildId, UserId = userId, EndsAt = endsAt
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to track a timeout for {UserId} in {GuildId}", userId, guildId);
        }
    }

    /// <summary>
    ///     Stops watching a timeout that was removed before it ran out.
    /// </summary>
    /// <param name="guildId">The guild the member was timed out in.</param>
    /// <param name="userId">The member.</param>
    private async Task UntrackTimeoutAsync(ulong guildId, ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            await db.TrackedTimeouts.Where(x => x.GuildId == guildId && x.UserId == userId).DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to untrack a timeout for {UserId} in {GuildId}", userId, guildId);
        }
    }

    /// <summary>
    ///     Logs every watched timeout that has run out, including ones that ended while the bot was offline.
    /// </summary>
    private async Task ProcessEndedTimeoutsAsync()
    {
        if (!await timeoutCheckLock.WaitAsync(0))
            return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var now = DateTime.UtcNow;
            var ended = await db.TrackedTimeouts.Where(x => x.EndsAt <= now).ToListAsync();
            foreach (var row in ended)
            {
                var guild = client.GetGuild(row.GuildId);
                if (guild is null)
                    continue;

                GuildEmbedColorService.Enter(row.GuildId);
                if (guild.GetUser(row.UserId)?.TimedOutUntil is { } current && current.UtcDateTime > now)
                {
                    await db.TrackedTimeouts.Where(x => x.Id == row.Id)
                        .Set(x => x.EndsAt, current.UtcDateTime)
                        .UpdateAsync();
                    continue;
                }

                if (await db.TrackedTimeouts.Where(x => x.Id == row.Id).DeleteAsync() == 0)
                    continue;

                if (!GuildLogSettings.TryGetValue(row.GuildId, out var logSetting) ||
                    logSetting.UserTimeoutRemovedId is null or 0 ||
                    guild.GetTextChannel(logSetting.UserTimeoutRemovedId.Value) is not { } channel)
                    continue;

                var gid = row.GuildId;
                var mention = guild.GetUser(row.UserId)?.Mention ?? $"<@{row.UserId}>";
                await SendLogAsync(channel, new ComponentBuilderV2()
                    .WithContainer([
                        new TextDisplayBuilder($"# {strings.LogUserTimeoutEnded(gid)}"),
                        new TextDisplayBuilder(strings.UserField(gid, mention, row.UserId) +
                                               strings.LogTimeoutUntilField(gid,
                                                   $"<t:{new DateTimeOffset(DateTime.SpecifyKind(row.EndsAt, DateTimeKind.Utc)).ToUnixTimeSeconds()}:f>"))
                    ], Mewdeko.OkColor));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process ended timeouts");
        }
        finally
        {
            timeoutCheckLock.Release();
        }
    }

    /// <summary>
    ///     Handles a message containing a Discord invite link.
    /// </summary>
    /// <param name="message">The received message.</param>
    private async Task OnInviteLinkPosted(SocketMessage message)
    {
        if (message.Channel is not SocketTextChannel guildChannel || IgnoresBot(guildChannel.Guild.Id, message.Author) ||
            message.Content.Length == 0)
            return;

        var matches = InviteRegex.Matches(message.Content);
        if (matches.Count == 0)
            return;

        if (!GuildLogSettings.TryGetValue(guildChannel.Guild.Id, out var logSetting) ||
            logSetting.InviteLinkPostedId is null or 0 ||
            IsChannelIgnored(guildChannel.Guild.Id, guildChannel.Id) ||
            guildChannel.Guild.GetTextChannel(logSetting.InviteLinkPostedId.Value) is not { } channel)
            return;

        var gid = guildChannel.Guild.Id;
        var invites = string.Join(", ", matches.Select(m => $"`{m.Value}`").Distinct());
        await SendLogAsync(channel, new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {strings.LogInviteLinkPosted(gid)}"),
                new TextDisplayBuilder(
                    strings.MessageAuthorField(gid, message.Author.Mention, message.Author.Id) +
                    strings.MessageChannelField(gid, guildChannel.Mention, guildChannel.Id) +
                    strings.LogInvitesField(gid, invites))
            ], Mewdeko.ErrorColor)
            .WithActionRow([
                new ButtonBuilder("Jump to Message", style: ButtonStyle.Link, url: message.GetJumpUrl())
            ]));
    }

    /// <summary>
    ///     Logs a boost when Discord posts the system message for one.
    /// </summary>
    /// <param name="message">The received message.</param>
    private async Task OnServerBoosted(SocketMessage message)
    {
        if (message.Type is not (MessageType.UserPremiumGuildSubscription
                or MessageType.UserPremiumGuildSubscriptionTier1 or MessageType.UserPremiumGuildSubscriptionTier2
                or MessageType.UserPremiumGuildSubscriptionTier3) ||
            message.Channel is not SocketTextChannel guildChannel)
            return;

        var guild = guildChannel.Guild;
        if (!GuildLogSettings.TryGetValue(guild.Id, out var logSetting) ||
            logSetting.ServerBoostedId is null or 0 ||
            guild.GetTextChannel(logSetting.ServerBoostedId.Value) is not { } channel)
            return;

        var gid = guild.Id;
        await SendLogAsync(channel, new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {strings.LogServerBoosted(gid)}"),
                new TextDisplayBuilder(strings.UserField(gid, message.Author.Mention, message.Author.Id) +
                                       strings.LogBoostTotalField(gid, guild.PremiumSubscriptionCount,
                                           guild.PremiumTier.ToString()))
            ], Mewdeko.OkColor));
    }

    /// <summary>
    ///     Logs a member joining a thread.
    /// </summary>
    /// <param name="user">The member who joined.</param>
    private Task OnThreadMemberJoined(SocketThreadUser user)
    {
        return LogThreadMemberAsync(user, true);
    }

    /// <summary>
    ///     Logs a member leaving a thread.
    /// </summary>
    /// <param name="user">The member who left.</param>
    private Task OnThreadMemberLeft(SocketThreadUser user)
    {
        return LogThreadMemberAsync(user, false);
    }

    private async Task LogThreadMemberAsync(SocketThreadUser user, bool joined)
    {
        var guild = user.Guild;
        if (IgnoresBot(guild.Id, user) || !GuildLogSettings.TryGetValue(guild.Id, out var logSetting) ||
            logSetting.ThreadMemberChangedId is null or 0 ||
            IsChannelIgnored(guild.Id, user.Thread.Id) ||
            guild.GetTextChannel(logSetting.ThreadMemberChangedId.Value) is not { } channel)
            return;

        var gid = guild.Id;
        await SendLogAsync(channel, new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder(
                    $"# {(joined ? strings.LogThreadMemberJoined(gid) : strings.LogThreadMemberLeft(gid))}"),
                new TextDisplayBuilder(strings.UserField(gid, user.Mention, user.Id) +
                                       strings.LogThreadField(gid, $"<#{user.Thread.Id}>", user.Thread.Id))
            ], joined ? Mewdeko.OkColor : Mewdeko.ErrorColor));
    }

    /// <summary>
    ///     Logs a change that Discord reports through the audit log.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="entry">The audit log entry.</param>
    /// <param name="channelOf">Picks the log channel from the guild's log settings.</param>
    /// <param name="title">The title of the log message.</param>
    private async Task LogAuditChangeAsync(SocketGuild guild, SocketAuditLogEntry entry,
        Func<LoggingV2, ulong?> channelOf, string title)
    {
        if (!GuildLogSettings.TryGetValue(guild.Id, out var logSetting) ||
            channelOf(logSetting) is not { } channelId || channelId == 0 ||
            guild.GetTextChannel(channelId) is not { } channel)
            return;

        var gid = guild.Id;
        var name = entry.Action.ToString();
        var created = name.Contains("Creat", StringComparison.Ordinal);
        var deleted = name.Contains("Delet", StringComparison.Ordinal);
        var text = strings.LogActionField(gid, created ? strings.LogActionCreated(gid)
            : deleted ? strings.LogActionDeleted(gid) : strings.LogActionUpdated(gid));

        string? target = null;
        ulong? targetChannel = null;
        switch (entry.Data)
        {
            case OverwriteCreateAuditLogData d:
                targetChannel = d.ChannelId;
                target = OverwriteTarget(d.Overwrite.TargetId, d.Overwrite.TargetType);
                break;
            case OverwriteUpdateAuditLogData d:
                targetChannel = d.ChannelId;
                target = OverwriteTarget(d.OverwriteTargetId, d.OverwriteType);
                break;
            case OverwriteDeleteAuditLogData d:
                targetChannel = d.ChannelId;
                target = OverwriteTarget(d.Overwrite.TargetId, d.Overwrite.TargetType);
                break;
            case WebhookCreateAuditLogData d:
                target = d.Name;
                targetChannel = d.ChannelId;
                break;
            case WebhookUpdateAuditLogData d:
                target = d.After.Name;
                break;
            case WebhookDeleteAuditLogData d:
                target = d.Name;
                targetChannel = d.ChannelId;
                break;
            case AutoModRuleCreatedAuditLogData d:
                target = d.Data.Name;
                break;
            case IntegrationCreatedAuditLogData d:
                target = IntegrationText(d.Integration.Name, d.Integration.Type);
                break;
            case IntegrationUpdatedAuditLogData d:
                target = IntegrationText(d.Integration.Name, d.Integration.Type);
                break;
            case IntegrationDeletedAuditLogData d:
                target = IntegrationText(d.Data.Name, d.Data.Type);
                break;
            case ScheduledEventUpdateAuditLogData d:
                target = d.After?.Name ?? d.ScheduledEvent?.Name ?? guild.GetEvent(d.Id)?.Name ?? d.Id.ToString();
                targetChannel = d.After?.ChannelId ?? d.ScheduledEvent?.ChannelId;
                break;
            case ScheduledEventDeleteAuditLogData d:
                target = d.Name ?? guild.GetEvent(d.Id)?.Name ?? d.Id.ToString();
                targetChannel = d.ChannelId;
                break;
            case Discord.Rest.AutoModRuleUpdatedAuditLogData d:
                target = d.After.Name;
                break;
            case AutoModRuleDeletedAuditLogData d:
                target = d.Data.Name;
                break;
            case StageInstanceCreateAuditLogData d:
                target = d.Topic;
                targetChannel = d.StageChannelId;
                break;
        }

        if (targetChannel is { } tc)
            text += strings.MessageChannelField(gid, $"<#{tc}>", tc);
        if (!string.IsNullOrWhiteSpace(target))
            text += strings.LogTargetField(gid, target);
        if (entry.User is { } by)
            text += strings.LogByField(gid, by.Mention, by.Id);
        if (!string.IsNullOrWhiteSpace(entry.Reason))
            text += strings.LogReasonField(gid, entry.Reason);

        await SendLogAsync(channel, new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {title}"),
                new TextDisplayBuilder(text)
            ], deleted ? Mewdeko.ErrorColor : Mewdeko.OkColor));
    }

    private static string IntegrationText(string? name, string? type)
    {
        return string.IsNullOrWhiteSpace(type) ? name ?? "" : $"{name} ({type})";
    }

    private Task OnEventStarted(SocketGuildEvent scheduledEvent)
    {
        return LogEventStateAsync(scheduledEvent, strings.LogEventStarted(scheduledEvent.Guild.Id), Mewdeko.OkColor);
    }

    private Task OnEventCancelled(SocketGuildEvent scheduledEvent)
    {
        return LogEventStateAsync(scheduledEvent, strings.LogEventCancelled(scheduledEvent.Guild.Id),
            Mewdeko.ErrorColor);
    }

    private Task OnEventCompleted(SocketGuildEvent scheduledEvent)
    {
        return LogEventStateAsync(scheduledEvent, strings.LogEventCompleted(scheduledEvent.Guild.Id),
            Mewdeko.OkColor);
    }

    private async Task LogEventStateAsync(SocketGuildEvent scheduledEvent, string title, Color color)
    {
        var guild = scheduledEvent.Guild;
        if (!GuildLogSettings.TryGetValue(guild.Id, out var logSetting) ||
            logSetting.ScheduledEventChangedId is null or 0 ||
            guild.GetTextChannel(logSetting.ScheduledEventChangedId.Value) is not { } channel)
            return;

        var gid = guild.Id;
        var text = strings.NameField(gid, scheduledEvent.Name);
        if (scheduledEvent.Channel is { } eventChannel)
            text += strings.MessageChannelField(gid, $"<#{eventChannel.Id}>", eventChannel.Id);
        else if (!string.IsNullOrWhiteSpace(scheduledEvent.Location))
            text += strings.LogLocationField(gid, scheduledEvent.Location);

        text += strings.LogStartsField(gid, $"<t:{scheduledEvent.StartTime.ToUnixTimeSeconds()}:f>");
        if (scheduledEvent.EndTime is { } end)
            text += strings.LogEndsField(gid, $"<t:{end.ToUnixTimeSeconds()}:f>");

        await SendLogAsync(channel, new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {title}"),
                new TextDisplayBuilder(text)
            ], color));
    }

    private static string OverwriteTarget(ulong id, PermissionTarget type)
    {
        return type == PermissionTarget.Role ? $"<@&{id}>" : $"<@{id}>";
    }

    /// <summary>
    ///     Toggles whether a channel is ignored for logging
    /// </summary>
    public async Task<IgnoreResult> LogIgnore(ulong guildId, ulong channelId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get or create the ignored channels set for this guild
            var ignoredChannels = ignoredChannelsCache.GetOrAdd(guildId, _ => new HashSet<ulong>());

            // Check if channel is already ignored
            var existing = await db.GetTable<LogIgnoredChannel>()
                .FirstOrDefaultAsync(lic => lic.GuildId == guildId && lic.ChannelId == channelId);

            if (existing != null)
            {
                // Remove from database
                await db.DeleteAsync(existing);

                // Remove from cache
                ignoredChannels.Remove(channelId);

                return IgnoreResult.Removed;
            }
            else
            {
                // Add to database
                await db.InsertAsync(new LogIgnoredChannel
                {
                    GuildId = guildId, ChannelId = channelId, DateAdded = DateTime.UtcNow
                });

                // Add to cache
                ignoredChannels.Add(channelId);

                return IgnoreResult.Added;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error toggling ignore status for channel {ChannelId} in guild {GuildId}",
                channelId, guildId);
            return IgnoreResult.Error;
        }
    }

    /// <summary>
    ///     Gets all ignored channels for a guild
    /// </summary>
    public async Task<HashSet<ulong>> GetIgnoredChannels(ulong guildId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var ignoredChannels = await db.GetTable<LogIgnoredChannel>()
                .Where(lic => lic.GuildId == guildId)
                .Select(lic => lic.ChannelId)
                .ToListAsync();

            return ignoredChannels.ToHashSet();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting ignored channels for guild {GuildId}", guildId);
            return new HashSet<ulong>();
        }
    }

    /// <summary>
    ///     Checks if a channel is ignored for logging
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="channelId">The channelid identifier.</param>
    public bool IsChannelIgnored(ulong guildId, ulong channelId)
    {
        if (ignoredChannelsCache.TryGetValue(guildId, out var ignoredChannels))
            return ignoredChannels.Contains(channelId);

        return false;
    }

    /// <summary>
    ///     Updates the ignored channels for a guild and fires events
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="channels">The channels identifier.</param>
    public async Task UpdateIgnoredChannelsAsync(ulong guildId, HashSet<ulong> channels)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Remove existing entries
            await db.GetTable<LogIgnoredChannel>()
                .Where(lic => lic.GuildId == guildId)
                .DeleteAsync();

            // Add new entries
            foreach (var channelId in channels)
            {
                await db.InsertAsync(new LogIgnoredChannel
                {
                    GuildId = guildId, ChannelId = channelId, DateAdded = DateTime.UtcNow
                }).ConfigureAwait(false);
            }

            // Update cache
            ignoredChannelsCache[guildId] = new HashSet<ulong>(channels);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating ignored channels for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Handles the event when an invite is created.
    /// </summary>
    /// <param name="invite">The invite that was created.</param>
    private async Task OnInviteCreated(SocketInvite invite)
    {
        if (invite.Guild == null) return;

        if (GuildLogSettings.TryGetValue(invite.Guild.Id, out var logSetting))
        {
            if (logSetting.InviteCreatedId is null or 0)
                return;

            var channel = invite.Guild.GetTextChannel(logSetting.InviteCreatedId.Value);
            if (channel is null)
                return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.InviteCreated(invite.Guild.Id)}")
                ], Mewdeko.OkColor);

            components.WithSeparator();
            components.WithTextDisplay("\n**Invite Details**");
            components.WithTextDisplay(
                $"`Invite Code:` {invite.Code}\n" +
                $"`Channel:` <#{invite.Channel.Id}> ({invite.Channel.Name}) | {invite.Channel.Id}\n" +
                $"`Created By:` {invite.Inviter?.Mention ?? "Unknown"} | {invite.Inviter?.Id.ToString() ?? "N/A"}\n" +
                $"`Max Uses:` {(invite.MaxUses == 0 ? "Unlimited" : invite.MaxUses.ToString())}\n" +
                $"`Max Age:` {(invite.MaxAge == 0 ? "Never" : TimeSpan.FromSeconds(invite.MaxAge).ToString(@"dd\.hh\:mm\:ss"))}\n" +
                $"`Temporary:` {invite.IsTemporary}\n" +
                $"`Created At:` {invite.CreatedAt:dd/MM/yyyy HH:mm:ss}");

            components.WithActionRow([
                new ButtonBuilder("Copy Invite URL", style: ButtonStyle.Link,
                    url: invite.Url)
            ]);

            await channel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when an invite is deleted.
    /// </summary>
    /// <param name="channel">The channel where the invite was deleted.</param>
    /// <param name="inviteCode">The code of the deleted invite.</param>
    private async Task OnInviteDeleted(SocketGuildChannel channel, string inviteCode)
    {
        if (GuildLogSettings.TryGetValue(channel.Guild.Id, out var logSetting))
        {
            if (logSetting.InviteDeletedId is null or 0)
                return;

            var logChannel = channel.Guild.GetTextChannel(logSetting.InviteDeletedId.Value);
            if (logChannel is null)
                return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.InviteDeletedTitle(channel.Guild.Id)}")
                ], Mewdeko.ErrorColor);

            components.WithSeparator();
            components.WithTextDisplay("\n**Invite Details**");
            components.WithTextDisplay(
                $"`Invite Code:` {inviteCode}\n" +
                $"`Channel:` <#{channel.Id}> ({channel.Name}) | {channel.Id}\n" +
                $"`Deleted At:` {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}");

            await logChannel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", logChannel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when messages are bulk deleted.
    /// </summary>
    /// <param name="messages">The collection of deleted messages.</param>
    /// <param name="channel">The channel where messages were deleted.</param>
    private async Task OnMessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
        Cacheable<IMessageChannel, ulong> channel)
    {
        if (!channel.HasValue || channel.Value is not SocketTextChannel guildChannel) return;

        // Check if channel is ignored
        if (IsChannelIgnored(guildChannel.Guild.Id, guildChannel.Id))
            return;

        if (GuildLogSettings.TryGetValue(guildChannel.Guild.Id, out var logSetting))
        {
            if (logSetting.MessagesBulkDeletedId is null or 0)
                return;

            var logChannel = guildChannel.Guild.GetTextChannel(logSetting.MessagesBulkDeletedId.Value);
            if (logChannel is null)
                return;

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {strings.MessagesBulkDeleted(guildChannel.Guild.Id)}")
                ], Mewdeko.ErrorColor);

            components.WithSeparator();
            components.WithTextDisplay("\n**Bulk Deletion Details**");
            components.WithTextDisplay(
                $"`Messages Deleted:` {messages.Count}\n" +
                $"`Channel:` <#{guildChannel.Id}> ({guildChannel.Name}) | {guildChannel.Id}\n" +
                $"`Deleted At:` {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}");

            // Count how many messages had content vs were cached
            var cachedMessages = messages.Where(m => m.HasValue).ToList();
            var uncachedCount = messages.Count - cachedMessages.Count;

            components.WithSeparator();
            components.WithTextDisplay("\n**Message Information**");
            components.WithTextDisplay(
                $"`Cached Messages:` {cachedMessages.Count}\n" +
                $"`Uncached Messages:` {uncachedCount}\n" +
                $"`Oldest Cached Message:` {(cachedMessages.Any() ? cachedMessages.Min(m => m.Value.Timestamp).ToString("dd/MM/yyyy HH:mm:ss") : "N/A")}\n" +
                $"`Newest Cached Message:` {(cachedMessages.Any() ? cachedMessages.Max(m => m.Value.Timestamp).ToString("dd/MM/yyyy HH:mm:ss") : "N/A")}");

            await logChannel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", logChannel.Guild.Id);
        }
    }

    /// <summary>
    ///     Handles the event when a reaction is added to a message.
    /// </summary>
    /// <param name="message">The cached message.</param>
    /// <param name="channel">The channel containing the message.</param>
    /// <param name="reaction">The reaction that was added.</param>
    private Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (!channel.HasValue || channel.Value is not SocketTextChannel guildChannel) return Task.CompletedTask;
        var reactionUser = reaction.User.GetValueOrDefault();
        if (IgnoresBot(guildChannel.Guild.Id, reactionUser)) return Task.CompletedTask;

        // Check if channel is ignored
        if (IsChannelIgnored(guildChannel.Guild.Id, guildChannel.Id))
            return Task.CompletedTask;

        AddReactionToBatch(message.Id, guildChannel.Id, guildChannel.Guild.Id, guildChannel.Name,
            reactionUser?.Id ?? 0, reaction.Emote, true);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the event when a reaction is removed from a message.
    /// </summary>
    /// <param name="message">The cached message.</param>
    /// <param name="channel">The channel containing the message.</param>
    /// <param name="reaction">The reaction that was removed.</param>
    private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (!channel.HasValue || channel.Value is not SocketTextChannel guildChannel) return Task.CompletedTask;
        var reactionUser = reaction.User.GetValueOrDefault();
        if (IgnoresBot(guildChannel.Guild.Id, reactionUser)) return Task.CompletedTask;

        // Check if channel is ignored
        if (IsChannelIgnored(guildChannel.Guild.Id, guildChannel.Id))
            return Task.CompletedTask;

        AddReactionToBatch(message.Id, guildChannel.Id, guildChannel.Guild.Id, guildChannel.Name,
            reactionUser?.Id ?? 0, reaction.Emote, false);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Adds a reaction event to the batching system.
    /// </summary>
    private void AddReactionToBatch(ulong messageId, ulong channelId, ulong guildId, string channelName, ulong userId,
        IEmote emote, bool isAdded)
    {
        if (userId == 0) return; // Invalid user

        var batch = reactionBatches.GetOrAdd(messageId, _ => new MessageReactionBatch
        {
            MessageId = messageId, ChannelId = channelId, GuildId = guildId, ChannelName = channelName
        });

        var emoteKey = emote.ToString() ?? "Unknown";

        lock (batch)
        {
            if (isAdded)
            {
                if (!batch.ReactionsAdded.ContainsKey(emoteKey))
                    batch.ReactionsAdded[emoteKey] = new List<ulong>();
                if (!batch.ReactionsAdded[emoteKey].Contains(userId))
                    batch.ReactionsAdded[emoteKey].Add(userId);
            }
            else
            {
                if (!batch.ReactionsRemoved.ContainsKey(emoteKey))
                    batch.ReactionsRemoved[emoteKey] = new List<ulong>();
                if (!batch.ReactionsRemoved[emoteKey].Contains(userId))
                    batch.ReactionsRemoved[emoteKey].Add(userId);
            }
        }
    }

    /// <summary>
    ///     Timer callback to process batched reaction events.
    /// </summary>
    private void ProcessReactionBatches(object? state)
    {
        var batchesToProcess = new List<MessageReactionBatch>();

        // Extract batches older than 5 seconds
        var cutoffTime = DateTime.UtcNow.AddSeconds(-5);
        var keysToRemove = new List<ulong>();

        foreach (var kvp in reactionBatches)
        {
            if (kvp.Value.FirstEventTime <= cutoffTime && kvp.Value.HasChanges)
            {
                batchesToProcess.Add(kvp.Value);
                keysToRemove.Add(kvp.Key);
            }
        }

        // Remove processed batches
        foreach (var key in keysToRemove)
        {
            reactionBatches.TryRemove(key, out _);
        }

        // Process each batch
        foreach (var batch in batchesToProcess)
        {
            _ = ProcessReactionBatch(batch);
        }
    }

    /// <summary>
    ///     Processes a single reaction batch and sends the log message.
    /// </summary>
    private async Task ProcessReactionBatch(MessageReactionBatch batch)
    {
        GuildEmbedColorService.Enter(batch.GuildId);
        if (!GuildLogSettings.TryGetValue(batch.GuildId, out var logSetting))
            return;

        if (logSetting.ReactionEventsId is null or 0)
            return;

        var logChannel = client.GetGuild(batch.GuildId)?.GetTextChannel(logSetting.ReactionEventsId.Value);
        if (logChannel is null)
            return;

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder("# Reaction Events")
            ], Mewdeko.OkColor);

        components.WithSeparator();
        components.WithTextDisplay("\n**Reaction Summary**");

        var summary =
            $"`Message:` <#{batch.ChannelId}>: [Jump to Message](https://discord.com/channels/{batch.GuildId}/{batch.ChannelId}/{batch.MessageId})\n" +
            $"`Channel:` <#{batch.ChannelId}> ({batch.ChannelName}) | {batch.ChannelId}\n" +
            $"`Total Added:` {batch.TotalAdded}\n" +
            $"`Total Removed:` {batch.TotalRemoved}";
        components.WithTextDisplay(summary);

        // Show detailed breakdown if there are changes
        if (batch.ReactionsAdded.Any())
        {
            components.WithSeparator();
            components.WithTextDisplay("\n**Reactions Added**");
            var addedDetails = string.Join("\n", batch.ReactionsAdded.Select(kvp =>
                $"`{kvp.Key}`: {kvp.Value.Count} users ({string.Join(", ", kvp.Value.Select(u => $"<@{u}>"))})"));
            components.WithTextDisplay(addedDetails.TrimTo(1000));
        }

        if (batch.ReactionsRemoved.Any())
        {
            components.WithSeparator();
            components.WithTextDisplay("\n**Reactions Removed**");
            var removedDetails = string.Join("\n", batch.ReactionsRemoved.Select(kvp =>
                $"`{kvp.Key}`: {kvp.Value.Count} users ({string.Join(", ", kvp.Value.Select(u => $"<@{u}>"))})"));
            components.WithTextDisplay(removedDetails.TrimTo(1000));
        }

        try
        {
            await logChannel.SendMessageAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
            collector.Feature("logging", logChannel.Guild.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending reaction batch log for message {MessageId} in guild {GuildId}",
                batch.MessageId, batch.GuildId);
        }
    }

    /// <summary>
    ///     Represents a batched collection of reaction events for a specific message
    /// </summary>
    public class MessageReactionBatch
    {
        /// <summary>
        ///     The ID of the message that had reaction changes
        /// </summary>
        public ulong MessageId { get; set; }

        /// <summary>
        ///     The ID of the channel containing the message
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        ///     The ID of the guild containing the message
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        ///     The name of the channel containing the message
        /// </summary>
        public string ChannelName { get; set; } = "";

        /// <summary>
        ///     When the first reaction event in this batch occurred
        /// </summary>
        public DateTime FirstEventTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        ///     Track reactions added by emote -> list of users who added them
        /// </summary>
        public Dictionary<string, List<ulong>> ReactionsAdded { get; set; } = new();

        /// <summary>
        ///     Track reactions removed by emote -> list of users who removed them
        /// </summary>
        public Dictionary<string, List<ulong>> ReactionsRemoved { get; set; } = new();

        /// <summary>
        ///     Gets total count of reaction additions in this batch
        /// </summary>
        public int TotalAdded
        {
            get
            {
                return ReactionsAdded.Values.Sum(users => users.Count);
            }
        }

        /// <summary>
        ///     Gets total count of reaction removals in this batch
        /// </summary>
        public int TotalRemoved
        {
            get
            {
                return ReactionsRemoved.Values.Sum(users => users.Count);
            }
        }

        /// <summary>
        ///     Gets whether this batch has any reaction changes
        /// </summary>
        public bool HasChanges
        {
            get
            {
                return TotalAdded > 0 || TotalRemoved > 0;
            }
        }
    }
}