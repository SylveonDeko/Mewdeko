using System.IO;
using System.Net.Http;
using Discord.Rest;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Moderation.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service for managing log commands.
/// </summary>
public class LogCommandService : INService, IReadyExecutor
{
    /// <summary>
    ///     Log category types.
    /// </summary>
    public enum LogCategoryTypes
    {
        /// <summary>
        ///     All events.
        /// </summary>
        All,

        /// <summary>
        ///     All events related to users.
        /// </summary>
        Users,

        /// <summary>
        ///     All events related to threads.
        /// </summary>
        Threads,

        /// <summary>
        ///     All events related to roles.
        /// </summary>
        Roles,

        /// <summary>
        ///     All events related to the server.
        /// </summary>
        Server,

        /// <summary>
        ///     All events related to messages.
        /// </summary>
        Channel,

        /// <summary>
        ///     All events related to messages.
        /// </summary>
        Messages,

        /// <summary>
        ///     All events related to moderation.
        /// </summary>
        Moderation,

        /// <summary>
        ///     Sets all events to none.
        /// </summary>
        None
    }

    /// <summary>
    ///     Dictionary of log types.
    /// </summary>
    public enum LogType
    {
        /// <summary>
        ///     The log type is custom, like user banned due to antiraid or other anti measures in the bot.
        /// </summary>
        Other,

        /// <summary>
        ///     An event was created in the guild.
        /// </summary>
        EventCreated,

        /// <summary>
        ///     A role was updated in the guild.
        /// </summary>
        RoleUpdated,

        /// <summary>
        ///     A role was created in the guild.
        /// </summary>
        RoleCreated,

        /// <summary>
        ///     A role was deleted in the guild.
        /// </summary>
        RoleDeleted,

        /// <summary>
        ///     The guild was updated.
        /// </summary>
        ServerUpdated,

        /// <summary>
        ///     A thread was created in the guild.
        /// </summary>
        ThreadCreated,

        /// <summary>
        ///     A user had a role added to them.
        /// </summary>
        UserRoleAdded,

        /// <summary>
        ///     A user had a role removed from them.
        /// </summary>
        UserRoleRemoved,

        /// <summary>
        ///     A user's username was updated.
        /// </summary>
        UsernameUpdated,

        /// <summary>
        ///     A user's nickname was updated.
        /// </summary>
        NicknameUpdated,

        /// <summary>
        ///     A thread was deleted in the guild.
        /// </summary>
        ThreadDeleted,

        /// <summary>
        ///     A thread was updated in the guild.
        /// </summary>
        ThreadUpdated,

        /// <summary>
        ///     A message was updated in the guild.
        /// </summary>
        MessageUpdated,

        /// <summary>
        ///     A message was deleted in the guild.
        /// </summary>
        MessageDeleted,

        /// <summary>
        ///     A user joined the guild.
        /// </summary>
        UserJoined,

        /// <summary>
        ///     A user left the guild.
        /// </summary>
        UserLeft,

        /// <summary>
        ///     A user was updated.
        /// </summary>
        UserBanned,

        /// <summary>
        ///     A user was unbanned.
        /// </summary>
        UserUnbanned,

        /// <summary>
        ///     A user was updated.
        /// </summary>
        UserUpdated,

        /// <summary>
        ///     A channel was created in the guild.
        /// </summary>
        ChannelCreated,

        /// <summary>
        ///     A channel was destroyed in the guild.
        /// </summary>
        ChannelDestroyed,

        /// <summary>
        ///     A channel was updated in the guild.
        /// </summary>
        ChannelUpdated,

        /// <summary>
        ///     A user's voice presence was updated.
        /// </summary>
        VoicePresence,

        /// <summary>
        ///     A user's used TTS in a voice channel.
        /// </summary>
        VoicePresenceTts,

        /// <summary>
        ///     A user was muted.
        /// </summary>
        UserMuted
    }

    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbProvider;


    /// <summary>
    ///     Constructs a new instance of the NewLogCommandService.
    /// </summary>
    /// <param name="db">The database service.</param>
    /// <param name="cache">The data cache.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="handler">The event handler.</param>
    /// <param name="muteService">The mute service.</param>
    public LogCommandService(DbContextProvider dbProvider, IDataCache cache, DiscordShardedClient client,
        EventHandler handler,
        MuteService muteService)
    {
        this.dbProvider = dbProvider;
        this.cache = cache;
        this.client = client;

        // Register event handlers
        handler.EventCreated += OnEventCreated;
        handler.RoleUpdated += OnRoleUpdated;
        handler.RoleCreated += OnRoleCreated;
        handler.RoleDeleted += OnRoleDeleted;
        handler.GuildUpdated += OnGuildUpdated;
        handler.ThreadCreated += OnThreadCreated;
        handler.GuildMemberUpdated += OnUserRoleAdded;
        handler.GuildMemberUpdated += OnUserRoleRemoved;
        handler.UserUpdated += OnUsernameUpdated;
        handler.GuildMemberUpdated += OnNicknameUpdated;
        handler.ThreadDeleted += OnThreadDeleted;
        handler.ThreadUpdated += OnThreadUpdated;
        handler.MessageUpdated += OnMessageUpdated;
        handler.MessageDeleted += OnMessageDeleted;
        handler.UserJoined += OnUserJoined;
        handler.UserLeft += OnUserLeft;
        handler.UserUpdated += OnUserUpdated;
        handler.ChannelCreated += OnChannelCreated;
        handler.ChannelDestroyed += OnChannelDestroyed;
        handler.ChannelUpdated += OnChannelUpdated;
        handler.UserVoiceStateUpdated += OnVoicePresence;
        handler.UserVoiceStateUpdated += OnVoicePresenceTts;
        handler.AuditLogCreated += OnAuditLogCreated;
        muteService.UserMuted += OnUserMuted;
        muteService.UserUnmuted += OnUserUnmuted;

        // Load guild configurations from the database
    }

    /// <summary>
    ///     Dictionary of log settings for each guild.
    /// </summary>
    public ConcurrentDictionary<ulong, LogSetting> GuildLogSettings { get; set; }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildIds = client.Guilds.Select(x => x.Id).ToList();
        var configs = await dbContext.GuildConfigs
            .AsQueryable()
            .Include(gc => gc.LogSetting)
            .ThenInclude(ls => ls.IgnoredChannels)
            .Where(x => guildIds.Contains(x.GuildId))
            .ToListAsyncEF();

        // Store the log settings in a concurrent dictionary for fast access
        GuildLogSettings = configs
            .ToDictionary(g => g.GuildId, g => g.LogSetting)
            .ToConcurrent();
    }

    /// <summary>
    ///     Handles the creation of audit logs.
    /// </summary>
    /// <param name="args">The audit log entry.</param>
    /// <param name="arsg2">The guild where the audit log was created.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnAuditLogCreated(SocketAuditLogEntry args, SocketGuild arsg2)
    {
        if (args.Action == ActionType.Ban)
        {
            var data = args.Data as BanAuditLogData;
            await OnUserBanned(data.Target, arsg2, args.User);
        }

        if (args.Action == ActionType.Unban)
        {
            var data = args.Data as UnbanAuditLogData;
            await OnUserUnbanned(data.Target, arsg2, args.User);
        }
    }

    /// <summary>
    ///     Handles the creation of a role.
    /// </summary>
    /// <param name="args">The role that was created.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnRoleCreated(SocketRole args)
    {
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            if (logSetting.RoleCreatedId is null or 0)
                return;

            var channel = args.Guild.GetTextChannel(logSetting.RoleCreatedId.Value);

            if (channel is null)
                return;

            await Task.Delay(500);

            var auditLogs = await args.Guild.GetAuditLogsAsync(1, actionType: ActionType.RoleCreated).FlattenAsync();

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Role Created")
                .WithDescription($"`Name:` {args.Name}\n" +
                                 $"`Id:` {args.Id}\n" +
                                 $"`Color:` {args.Color}\n" +
                                 $"`Hoisted:` {args.IsHoisted}\n" +
                                 $"`Mentionable:` {args.IsMentionable}\n" +
                                 $"`Position:` {args.Position}\n" +
                                 $"`Permissions:` {string.Join(", ", args.Permissions.ToList())}\n" +
                                 $"`Created By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}\n" +
                                 $"`Managed:` {args.IsManaged}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a guild is updated.
    /// </summary>
    /// <param name="args">The updated guild.</param>
    /// <param name="arsg2">The original guild before the update.</param>
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

            var auditLogs = await args.GetAuditLogsAsync(1, actionType: ActionType.GuildUpdated).FlattenAsync();

            var eb = new EmbedBuilder();

            if (args.Name != arsg2.Name)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Name Updated")
                    .WithDescription($"`New Name:` {arsg2.Name}\n" +
                                     $"`Old Name:` {args.Name}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.IconUrl != arsg2.IconUrl)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Icon Updated")
                    .WithDescription(
                        $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}")
                    .WithThumbnailUrl(args.IconUrl)
                    .WithImageUrl(arsg2.IconUrl);
            }

            if (args.BannerUrl != arsg2.BannerUrl)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Banner Updated")
                    .WithDescription(
                        $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}")
                    .WithThumbnailUrl(args.BannerUrl)
                    .WithImageUrl(arsg2.BannerUrl);
            }

            if (args.SplashUrl != arsg2.SplashUrl)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Splash Updated")
                    .WithDescription(
                        $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}")
                    .WithThumbnailUrl(args.SplashUrl)
                    .WithImageUrl(arsg2.SplashUrl);
            }

            if (args.VanityURLCode != arsg2.VanityURLCode)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Vanity URL Updated")
                    .WithDescription($"`New Vanity URL:` {arsg2.VanityURLCode}\n" +
                                     $"`Old Vanity URL:` {args.VanityURLCode}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.OwnerId != arsg2.OwnerId)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Owner Updated")
                    .WithDescription($"`New Owner:` {arsg2.Owner.Mention} | {arsg2.Owner.Id}\n" +
                                     $"`Old Owner:` {args.Owner.Mention} | {args.Owner.Id}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.AFKChannel != arsg2.AFKChannel)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server AFK Channel Updated")
                    .WithDescription($"`New AFK Channel:` {arsg2.AFKChannel.Mention} | {arsg2.AFKChannel.Id}\n" +
                                     $"`Old AFK Channel:` {args.AFKChannel.Mention} | {args.AFKChannel.Id}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.AFKTimeout != arsg2.AFKTimeout)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server AFK Timeout Updated")
                    .WithDescription($"`New AFK Timeout:` {arsg2.AFKTimeout}\n" +
                                     $"`Old AFK Timeout:` {args.AFKTimeout}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.DefaultMessageNotifications != arsg2.DefaultMessageNotifications)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Default Message Notifications Updated")
                    .WithDescription($"`New Default Message Notifications:` {arsg2.DefaultMessageNotifications}\n" +
                                     $"`Old Default Message Notifications:` {args.DefaultMessageNotifications}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.ExplicitContentFilter != arsg2.ExplicitContentFilter)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Explicit Content Filter Updated")
                    .WithDescription($"`New Explicit Content Filter:` {arsg2.ExplicitContentFilter}\n" +
                                     $"`Old Explicit Content Filter:` {args.ExplicitContentFilter}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.MfaLevel != arsg2.MfaLevel)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server MFA Level Updated")
                    .WithDescription($"`New MFA Level:` {arsg2.MfaLevel}\n" +
                                     $"`Old MFA Level:` {args.MfaLevel}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.VerificationLevel != arsg2.VerificationLevel)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Verification Level Updated")
                    .WithDescription($"`New Verification Level:` {arsg2.VerificationLevel}\n" +
                                     $"`Old Verification Level:` {args.VerificationLevel}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.SystemChannel != arsg2.SystemChannel)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server System Channel Updated")
                    .WithDescription(
                        $"`New System Channel:` {arsg2.SystemChannel.Mention} | {arsg2.SystemChannel.Id}\n" +
                        $"`Old System Channel:` {args.SystemChannel.Mention} | {args.SystemChannel.Id}\n" +
                        $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.RulesChannel != arsg2.RulesChannel)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Rules Channel Updated")
                    .WithDescription($"`New Rules Channel:` {arsg2.RulesChannel.Mention} | {arsg2.RulesChannel.Id}\n" +
                                     $"`Old Rules Channel:` {args.RulesChannel.Mention} | {args.RulesChannel.Id}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.PublicUpdatesChannel != arsg2.PublicUpdatesChannel)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Public Updates Channel Updated")
                    .WithDescription(
                        $"`New Public Updates Channel:` {arsg2.PublicUpdatesChannel.Mention} | {arsg2.PublicUpdatesChannel.Id}\n" +
                        $"`Old Public Updates Channel:` {args.PublicUpdatesChannel.Mention} | {args.PublicUpdatesChannel.Id}\n" +
                        $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.MaxVideoChannelUsers != arsg2.MaxVideoChannelUsers)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Max Video Channel Users Updated")
                    .WithDescription($"`New Max Video Channel Users:` {arsg2.MaxVideoChannelUsers}\n" +
                                     $"`Old Max Video Channel Users:` {args.MaxVideoChannelUsers}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.MaxMembers != arsg2.MaxMembers)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Server Max Members Updated")
                    .WithDescription($"`New Max Members:` {arsg2.MaxMembers}\n" +
                                     $"`Old Max Members:` {args.MaxMembers}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a role is deleted in a guild.
    /// </summary>
    /// <param name="args">The role that was deleted.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnRoleDeleted(SocketRole args)
    {
        // Try to get the log settings for the guild
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            // If no log setting for role deletion, return
            if (logSetting.RoleDeletedId is null or 0)
                return;

            // Get the text channel for logging
            var channel = args.Guild.GetTextChannel(logSetting.RoleDeletedId.Value);

            // If the channel is null, return
            if (channel is null)
                return;

            // Wait for a short period to ensure all events are processed
            await Task.Delay(500);

            // Get the audit logs for the guild
            var auditLogs = await args.Guild.GetAuditLogsAsync(1, actionType: ActionType.GuildUpdated).FlattenAsync();

            // Create an embed builder for the message
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Role Deleted")
                .WithDescription($"`Role:` {args.Name}\n" +
                                 $"`Deleted By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}\n" +
                                 $"`Deleted At:` {DateTime.UtcNow}\n" +
                                 $"`Members:` {args.Members.Count()}");

            // Send the message to the channel
            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a role is updated in a guild.
    /// </summary>
    /// <param name="args">The updated role.</param>
    /// <param name="arsg2">The original role before the update.</param>
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

            var auditLogs = await args.Guild.GetAuditLogsAsync(1, actionType: ActionType.RoleUpdated).FlattenAsync();

            var eb = new EmbedBuilder();

            if (args.Name != arsg2.Name)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Name Updated")
                    .WithDescription($"`New Role Name:` {arsg2.Name}\n" +
                                     $"`Old Role Name:` {args.Name}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.Color != arsg2.Color)
            {
                eb = new EmbedBuilder()
                    .WithColor(arsg2.Color)
                    .WithTitle("Role Color Updated")
                    .WithDescription($"`Role:` {args.Mention} | {args.Id}" +
                                     $"`New Role Color:` {arsg2.Color}\n" +
                                     $"`Old Role Color:` {args.Color}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.IsHoisted != arsg2.IsHoisted)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Property Hoisted Updated")
                    .WithDescription($"`Role:` {args.Mention} | {args.Id}" +
                                     $"`New Role Hoisted:` {arsg2.IsHoisted}\n" +
                                     $"`Old Role Hoisted:` {args.IsHoisted}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.IsMentionable != arsg2.IsMentionable)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Property Mentionable Updated")
                    .WithDescription($"`Role:` {args.Mention} | {args.Id}" +
                                     $"`New Role Mentionable:` {arsg2.IsMentionable}\n" +
                                     $"`Old Role Mentionable:` {args.IsMentionable}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.IsManaged != arsg2.IsManaged)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Property Managed Updated")
                    .WithDescription($"`Role:` {args.Mention} | {args.Id}" +
                                     $"`New Role Managed:` {arsg2.IsManaged}\n" +
                                     $"`Old Role Managed:` {args.IsManaged}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.Position != arsg2.Position)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Position Updated")
                    .WithDescription($"`Role:` {args.Mention} | {args.Id}" +
                                     $"`New Role Position:` {arsg2.Position}\n" +
                                     $"`Old Role Position:` {args.Position}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (!arsg2.Permissions.Equals(args.Permissions))
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Permissions Updated")
                    .WithDescription($"`Role:` {args.Mention} | {args.Id}" +
                                     $"`New Role Permissions:` {arsg2.Permissions}\n" +
                                     $"`Old Role Permissions:` {args.Permissions}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.Icon != arsg2.Icon)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Icon Updated")
                    .WithDescription($"`Role:` {args.Mention} | {args.Id}" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}")
                    .WithThumbnailUrl(args.GetIconUrl())
                    .WithImageUrl(arsg2.GetIconUrl());
            }

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a new event is created in a guild.
    /// </summary>
    /// <param name="args">The event that was created.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnEventCreated(SocketGuildEvent args)
    {
        // Try to get the log settings for the guild
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            // If no log setting for event creation, return
            if (logSetting.EventCreatedId is null or 0)
                return;

            // Get the text channel for logging
            var channel = args.Guild.GetTextChannel(logSetting.EventCreatedId.Value);

            // If the channel is null, return
            if (channel is null)
                return;

            // Create an embed builder for the message
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Event Created")
                .WithDescription($"`Event:` {args.Name}\n" +
                                 $"`Created By:` {args.Creator.Mention} | {args.Creator.Id}\n" +
                                 $"`Created At:` {DateTime.UtcNow}\n" +
                                 $"`Description:` {args.Description}\n" +
                                 $"`Event Date:` {args.StartTime}\n" +
                                 $"`End Date:` {args.EndTime}\n" +
                                 $"`Event Location:` {args.Location}\n" +
                                 $"`Event Type:` {args.Type}\n" +
                                 $"`Event Id:` {args.Id}")
                .WithImageUrl(args.GetCoverImageUrl());

            // Send the message to the channel
            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles logging for when a thread is created.
    /// </summary>
    /// <param name="socketThreadChannel">The created thread channel.</param>
    private async Task OnThreadCreated(SocketThreadChannel socketThreadChannel)
    {
        if (GuildLogSettings.TryGetValue(socketThreadChannel.Guild.Id, out var logSetting))
        {
            if (logSetting.ThreadCreatedId is null or 0)
                return;

            var channel = socketThreadChannel.Guild.GetTextChannel(logSetting.ThreadCreatedId.Value);

            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Thread Created")
                .WithDescription($"`Name:` {socketThreadChannel.Name}\n" +
                                 $"`Created By:` {socketThreadChannel.Owner.Mention} | {socketThreadChannel.Owner.Id}\n" +
                                 $"`Created At:` {DateTime.UtcNow}\n" +
                                 $"`Thread Type:` {socketThreadChannel.Type}\n" +
                                 $"`Thread Tags:` {socketThreadChannel.AppliedTags}");

            await channel.SendMessageAsync(embed: eb.Build());
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

        var channel = arsg2.Guild.GetTextChannel(logSetting.UserRoleAddedId.Value);
        if (channel is null) return;

        await Task.Delay(500);
        var auditLogs = await arsg2.Guild.GetAuditLogsAsync(1, actionType: ActionType.MemberRoleUpdated)
            .FlattenAsync();

        var addedRoles = arsg2.Roles.Except(cacheable.Value.Roles);
        if (!addedRoles.Any()) return;

        var auditLog = auditLogs.LastOrDefault();
        if (auditLog == null) return;

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle("User Role(s) Added")
            .WithDescription($"`Role(s):` {string.Join(", ", addedRoles.Select(x => x.Mention))}\n" +
                             $"`Added By:` {auditLog.User.Mention} | {auditLog.User.Id}" +
                             $"\n`Added To:` {arsg2.Mention} | {arsg2.Id}");

        await channel.SendMessageAsync(embed: eb.Build());
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

        var channel = arsg2.Guild.GetTextChannel(logSetting.UserRoleRemovedId.Value);
        if (channel is null) return;

        await Task.Delay(500);
        var auditLogs = await arsg2.Guild.GetAuditLogsAsync(1, actionType: ActionType.MemberRoleUpdated)
            .FlattenAsync();

        var auditLog = auditLogs.LastOrDefault();
        if (auditLog == null) return;

        var removedRoles = cacheable.Value.Roles.Except(arsg2.Roles);
        if (!removedRoles.Any()) return;


        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle("User Role(s) Removed")
            .WithDescription($"`Role(s):` {string.Join(", ", removedRoles.Select(x => x.Mention))}\n" +
                             $"`Added By:` {auditLog.User.Mention} | {auditLog.User.Id}" +
                             $"\n`Removed From:` {arsg2.Mention} | {arsg2.Id}");

        await channel.SendMessageAsync(embed: eb.Build());
    }


    /// <summary>
    ///     Handles the event when a user updates their username.
    /// </summary>
    /// <param name="args">The user before they updated their username.</param>
    /// <param name="arsg2">The user after they updated their username.</param>
    private async Task OnUsernameUpdated(SocketUser args, SocketUser arsg2)
    {
        if (args is not SocketGuildUser user)
            return;
        if (args.Username.Equals(arsg2.Username))
            return;

        if (GuildLogSettings.TryGetValue(user.Guild.Id, out var logSetting))
        {
            if (logSetting.UsernameUpdatedId is null or 0)
                return;

            var channel = user.Guild.GetTextChannel(logSetting.UsernameUpdatedId.Value);

            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Username Updated")
                .WithDescription(
                    $"`Old Username:` {args.Username}\n" +
                    $"`New Username:` {arsg2.Username}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user updates their nickname.
    /// </summary>
    /// <param name="cacheable">The user before they updated their nickname</param>
    /// <param name="arsg2">The user after they updated their nickname</param>
    private async Task OnNicknameUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser arsg2)
    {
        if (!cacheable.HasValue) return;
        if (GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting))
        {
            if (cacheable.Value.Nickname.Equals(arsg2.Nickname))
                return;

            if (logSetting.NicknameUpdatedId is null or 0)
                return;

            var auditLogs = await arsg2.Guild.GetAuditLogsAsync(1, actionType: ActionType.MemberUpdated).FlattenAsync();

            var entry = auditLogs.FirstOrDefault();

            var channel = arsg2.Guild.GetTextChannel(logSetting.NicknameUpdatedId.Value);

            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Nickname Updated")
                .WithDescription(
                    $"`Old Nickname:` {cacheable.Value.Nickname ?? cacheable.Value.Username}\n" +
                    $"`New Nickname:` {arsg2.Nickname ?? arsg2.Username}" +
                    $"`Updated By:` {entry.User.Mention} | {entry.User.Id}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a thread gets deleted.
    /// </summary>
    /// <param name="args">The cached thread. May return null. See <see cref="Cacheable{TEntity,TId}" /></param>
    private async Task OnThreadDeleted(Cacheable<SocketThreadChannel, ulong> args)
    {
        if (!args.HasValue) return;
        if (GuildLogSettings.TryGetValue(args.Value.Guild.Id, out var logSetting))
        {
            if (logSetting.ThreadDeletedId is null or 0)
                return;

            var channel = args.Value.Guild.GetTextChannel(logSetting.ThreadDeletedId.Value);

            if (channel is null)
                return;

            var auditLogs = await args.Value.Guild.GetAuditLogsAsync(1, actionType: ActionType.ThreadDelete)
                .FlattenAsync();

            var entry = auditLogs.FirstOrDefault();

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Thread Deleted")
                .WithDescription(
                    $"`Thread Name:` {args.Value.Name}\n" +
                    $"`Thread Id:` {args.Value.Id}" +
                    $"`Deleted By:` {entry.User.Mention} | {entry.User.Id}");

            await channel.SendMessageAsync(embed: eb.Build());
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
        if (GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting))
        {
            if (logSetting.ThreadUpdatedId is null or 0)
                return;

            var channel = arsg2.Guild.GetTextChannel(logSetting.ThreadUpdatedId.Value);

            if (channel is null)
                return;

            var auditLogs = await arsg2.Guild.GetAuditLogsAsync(1, actionType: ActionType.ThreadUpdate).FlattenAsync();

            var entry = auditLogs.FirstOrDefault();

            var eb = new EmbedBuilder();

            if (oldThread.Name != arsg2.Name)
                eb.WithOkColor()
                    .WithTitle("Thread Name Updated")
                    .WithDescription(
                        $"`Old Thread Name:` {oldThread.Name}\n" +
                        $"`New Thread Name:` {arsg2.Name}" +
                        $"`Updated By:` {entry.User.Mention} | {entry.User.Id}");

            if (oldThread.IsArchived != arsg2.IsArchived)
                eb.WithOkColor()
                    .WithTitle("Thread Archival Status Updated")
                    .WithDescription($"Before: {oldThread.IsArchived}\n" +
                                     $"After: {arsg2.IsArchived}\n" +
                                     $"`Updated By:` {entry.User.Mention} | {entry.User.Id}");

            if (oldThread.IsLocked != arsg2.IsLocked)
                eb.WithOkColor()
                    .WithTitle("Thread Lock Status Updated")
                    .WithDescription($"Before: {oldThread.IsLocked}\n" +
                                     $"After: {arsg2.IsLocked}\n" +
                                     $"`Updated By:` {entry.User.Mention} | {entry.User.Id}");

            await channel.SendMessageAsync(embed: eb.Build());
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
        if (args3 is not SocketTextChannel guildChannel)
            return;
        if (cacheable.Value.Content.Equals(args2.Content))
            return;
        if (GuildLogSettings.TryGetValue(guildChannel.Guild.Id, out var logSetting))
        {
            if (logSetting.MessageUpdatedId is null or 0)
                return;

            var channel = guildChannel.Guild.GetTextChannel(logSetting.MessageUpdatedId.Value);

            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Message Updated")
                .WithDescription(
                    $"`Message Author:` {oldMessage.Author.Mention}\n" +
                    $"`Message Channel:` {guildChannel.Mention} | {guildChannel.Id}\n" +
                    $"`Message Id:` {oldMessage.Id}\n" +
                    $"`Old Message Content:` {oldMessage.Content}\n" +
                    $"`Updated Message Content:` {args2.Content}");

            var component = new ComponentBuilder()
                .WithButton("Jump to Message", style: ButtonStyle.Link, url: oldMessage.GetJumpUrl()).Build();

            await channel.SendMessageAsync(embed: eb.Build(), components: component);
        }
    }

    /// <summary>
    ///     Handles the event when a message is deleted.
    /// </summary>
    /// <param name="args">The cached message. May return null. See <see cref="Cacheable{TEntity,TId}" /></param>
    /// <param name="arsg2">The channel where the message was deleted</param>
    private async Task OnMessageDeleted(Cacheable<IMessage, ulong> args, Cacheable<IMessageChannel, ulong> arsg2)
    {
        if (!args.HasValue) return;
        if (args.Value is not SocketUserMessage message) return;
        if (args.Value.Channel is not SocketTextChannel guildChannel) return;
        if (GuildLogSettings.TryGetValue(guildChannel.Guild.Id, out var logSetting))
        {
            if (logSetting.MessageDeletedId is null or 0)
                return;

            var channel = guildChannel.Guild.GetTextChannel(logSetting.MessageDeletedId.Value);

            var auditLogs = await guildChannel.Guild.GetAuditLogsAsync(1, actionType: ActionType.MessageDeleted)
                .FlattenAsync();

            var entry = auditLogs.FirstOrDefault();

            if (entry.Data is not MessageDeleteAuditLogData data)
                return;

            var currentTime = DateTimeOffset.UtcNow;
            var timeThreshold = TimeSpan.FromSeconds(2);

            var deleteUser = data.ChannelId == message.Channel.Id &&
                             (currentTime - entry.CreatedAt) <= timeThreshold ? entry.User : message.Author;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Message Deleted")
                .WithDescription(
                    $"`Message Author:` {message.Author.Mention}\n" +
                    $"`Message Channel:` {guildChannel.Mention} | {guildChannel.Id}\n" +
                    $"`Message Content:` {message.Content}\n" +
                    $"`Deleted By:` {deleteUser.Mention} | {deleteUser.Id}");

            // Handle attachments
            if (message.Attachments.Count != 0)
            {
                eb.AddField("Attachments", $"{message.Attachments.Count} attachment(s) were included in this message.");

                foreach (var attachment in message.Attachments)
                {
                    if (IsImageAttachment(attachment.Filename))
                    {
                        try
                        {
                            // Download the image
                            using var client = new HttpClient();
                            var imageBytes = await client.GetByteArrayAsync(attachment.Url);

                            // Convert to Base64
                            var base64Image = Convert.ToBase64String(imageBytes);

                            // Reconstruct and upload the image
                            using var ms = new MemoryStream(Convert.FromBase64String(base64Image));
                            await channel.SendFileAsync(ms, attachment.Filename, "Deleted attachment:");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to process attachment: {ex.Message}");
                            eb.AddField("Attachment Processing Error", $"Failed to process {attachment.Filename}");
                        }
                    }
                    else
                    {
                        eb.AddField(attachment.Filename, attachment.Url);
                    }
                }
            }

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    private static bool IsImageAttachment(string filename)
    {
        var imageExtensions = new[]
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp"
        };
        return imageExtensions.Any(ext => filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Handles the event when a user joins a guild.
    /// </summary>
    /// <param name="guildUser">The user that joined the guild.</param>
    private async Task OnUserJoined(IGuildUser guildUser)
    {
        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.UserJoinedId is null or 0)
                return;

            var channel = await guildUser.Guild.GetTextChannelAsync(logSetting.UserJoinedId.Value);

            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("User Joined")
                .WithDescription(
                    $"`User:` {guildUser.Mention} | {guildUser.Id}\n" +
                    $"`Account Created:` {guildUser.CreatedAt:dd/MM/yyyy}\n" +
                    $"`Joined Server:` {guildUser.JoinedAt:dd/MM/yyyy}\n" +
                    $"`User Status:` {guildUser.Status}\n" +
                    $"`User Id:` {guildUser.Id}\n" +
                    $"`User Global Name:` {guildUser.GlobalName ?? guildUser.Username}")
                .WithThumbnailUrl(guildUser.RealAvatarUrl().ToString());

            var component = new ComponentBuilder()
                .WithButton("View User", style: ButtonStyle.Link, url: $"discord://-/users/{guildUser.Id}").Build();

            await channel.SendMessageAsync(components: component, embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user leaves a guild.
    /// </summary>
    /// <param name="guild">The guild the user left.</param>
    /// <param name="arsg2">The user that left the guild.</param>
    private async Task OnUserLeft(IGuild guild, IUser arsg2)
    {
        if (arsg2 is not SocketGuildUser usr) return;
        if (GuildLogSettings.TryGetValue(guild.Id, out var logSetting))
        {
            if (logSetting.UserLeftId is null or 0)
                return;

            var channel = await guild.GetTextChannelAsync(logSetting.UserLeftId.Value);

            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("User Left")
                .WithDescription(
                    $"`User:` {usr.Mention} | {usr.Id}\n" +
                    $"`User Id:` {usr.Id}\n" +
                    $"`User Global Name:` {arsg2.GlobalName ?? arsg2.Username}\n" +
                    $"`Account Created:` {usr.CreatedAt:dd/MM/yyyy}\n" +
                    $"`Joined Server:` {usr.JoinedAt:dd/MM/yyyy}\n" +
                    $"`Roles:` {string.Join(", ", usr.Roles.Select(x => x.Mention))}")
                .WithThumbnailUrl(arsg2.RealAvatarUrl().ToString());

            var component = new ComponentBuilder().WithButton("View User (May not work)", style: ButtonStyle.Link,
                url: $"discord://-/users/{arsg2.Id}").Build();

            await channel.SendMessageAsync(components: component, embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user is banned from a guild.
    /// </summary>
    /// <param name="args">The user that was banned.</param>
    /// <param name="arsg2">The guild the user was banned from.</param>
    /// <param name="bannedBy">The user that banned the user.</param>
    private async Task OnUserBanned(IUser args, SocketGuild arsg2, SocketUser bannedBy)
    {
        if (args is not SocketGuildUser usr) return;
        if (GuildLogSettings.TryGetValue(arsg2.Id, out var logSetting))
        {
            if (logSetting.UserBannedId is null or 0)
                return;

            var channel = usr.Guild.GetTextChannel(logSetting.UserBannedId.Value);

            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("User Banned")
                .WithDescription(
                    $"`User:` {args.Mention} | {args.Id}\n" +
                    $"`User Id:` {args.Id}\n" +
                    $"`User Global Name:` {args.GlobalName ?? args.Username}\n" +
                    $"`Account Created:` {args.CreatedAt:dd/MM/yyyy}\n" +
                    $"`Joined Server:` {usr.JoinedAt:dd/MM/yyyy}\n" +
                    $"`Roles:` {string.Join(", ", usr.Roles.Select(x => x.Mention))}\n" +
                    $"`Banned By:` {bannedBy.Mention} | {bannedBy.Id}")
                .WithThumbnailUrl(args.RealAvatarUrl().ToString());

            var component = new ComponentBuilder().WithButton("View User (May not work)", style: ButtonStyle.Link,
                url: $"discord://-/users/{args.Id}").Build();

            await channel.SendMessageAsync(components: component, embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user is unbanned from a guild.
    /// </summary>
    /// <param name="args">The user that was unbanned.</param>
    /// <param name="arsg2">The guild the user was unbanned from.</param>
    /// <param name="unbannedBy">The user that unbanned the user.</param>
    private async Task OnUserUnbanned(IUser args, SocketGuild arsg2, SocketUser unbannedBy)
    {
        if (GuildLogSettings.TryGetValue(arsg2.Id, out var logSetting))
        {
            if (logSetting.UserUnbannedId is null or 0)
                return;

            var channel = arsg2.GetTextChannel(logSetting.UserUnbannedId.Value);

            if (channel is null)
                return;


            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("User Unbanned")
                .WithDescription(
                    $"`User:` {args.Mention} | {args.Id}\n" +
                    $"`User Id:` {args.Id}" +
                    $"`User Global Name:` {args.GlobalName ?? args.Username}" +
                    $"`Account Created:` {args.CreatedAt:dd/MM/yyyy}\n" +
                    $"`Unbanned By:` {unbannedBy.Mention} | {unbannedBy.Id}")
                .WithThumbnailUrl(args.RealAvatarUrl().ToString());

            var component = new ComponentBuilder().WithButton("View User (May not work)", style: ButtonStyle.Link,
                url: $"discord://-/users/{args.Id}").Build();

            await channel.SendMessageAsync(components: component, embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user is updated.
    /// </summary>
    /// <param name="args">The user before the update.</param>
    /// <param name="arsg2">The user after the update.</param>
    private async Task OnUserUpdated(SocketUser args, SocketUser arsg2)
    {
        if (args is not SocketGuildUser usr) return;

        if (GuildLogSettings.TryGetValue(usr.Guild.Id, out var logSetting))
        {
            if (logSetting.UserUpdatedId is null or 0)
                return;

            var channel = usr.Guild.GetTextChannel(logSetting.UserUpdatedId.Value);

            if (channel is null)
                return;

            var eb = new EmbedBuilder();

            if (args.AvatarId != arsg2.AvatarId)
                eb.WithOkColor()
                    .WithTitle("User Avatar Updated")
                    .WithThumbnailUrl(args.RealAvatarUrl().ToString())
                    .WithImageUrl(arsg2.RealAvatarUrl().ToString());

            if (args.GlobalName != arsg2.GlobalName)
                eb.WithOkColor()
                    .WithTitle("User Global Name Updated")
                    .WithDescription(
                        $"`User:` {args.Mention} | {args.Id}\n" +
                        $"`User Id:` {args.Id}\n" +
                        $"`User Global Name:` {args.GlobalName ?? args.Username}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a channel is created in a guild.
    /// </summary>
    /// <param name="args">The channel that was created.</param>
    private async Task OnChannelCreated(SocketChannel args)
    {
        if (args is not SocketGuildChannel channel) return;

        if (GuildLogSettings.TryGetValue(channel.Guild.Id, out var logSetting))
        {
            if (logSetting.ChannelCreatedId is null or 0)
                return;

            var logChannel = channel.Guild.GetTextChannel(logSetting.ChannelCreatedId.Value);

            if (logChannel is null)
                return;

            string createdType;

            if (channel is SocketTextChannel)
                createdType = "Text";
            else
                switch (args)
                {
                    case SocketVoiceChannel:
                        createdType = "Voice";
                        break;
                    case SocketCategoryChannel:
                        createdType = "Category";
                        break;
                    case SocketNewsChannel:
                        createdType = "News";
                        break;
                    default:
                    {
                        createdType = channel is SocketStageChannel ? "Stage" : "Unknown";
                        break;
                    }
                }

            var auditLogs = await channel.Guild.GetAuditLogsAsync(1, actionType: ActionType.ChannelCreated)
                .FlattenAsync();

            var entry = auditLogs.FirstOrDefault();

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Channel Created")
                .WithDescription(
                    $"`Channel:` {channel.Name} | {channel.Id}\n" +
                    $"`Channel Created By:` {entry.User.Mention} | {entry.User.Id}\n" +
                    $"`Channel Created At:` {channel.CreatedAt:dd/MM/yyyy}\n" +
                    $"`Channel Type:` {createdType}");

            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a channel is destroyed/deleted in a guild.
    /// </summary>
    /// <param name="args">The channel that was destroyed.</param>
    private async Task OnChannelDestroyed(SocketChannel args)
    {
        if (args is not SocketGuildChannel channel) return;

        if (GuildLogSettings.TryGetValue(channel.Guild.Id, out var logSetting))
        {
            if (logSetting.ChannelDestroyedId is null or 0)
                return;

            var logChannel = channel.Guild.GetTextChannel(logSetting.ChannelDestroyedId.Value);

            if (logChannel is null)
                return;

            string createdType;

            if (channel is SocketTextChannel)
                createdType = "Text";
            else
                switch (args)
                {
                    case SocketVoiceChannel:
                        createdType = "Voice";
                        break;
                    case SocketCategoryChannel:
                        createdType = "Category";
                        break;
                    case SocketNewsChannel:
                        createdType = "News";
                        break;
                    default:
                    {
                        createdType = channel is SocketStageChannel ? "Stage" : "Unknown";
                        break;
                    }
                }

            var auditLogs = await channel.Guild.GetAuditLogsAsync(1, actionType: ActionType.ChannelDeleted)
                .FlattenAsync();

            var entry = auditLogs.FirstOrDefault();

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Channel Destroyed")
                .WithDescription(
                    $"`Channel:` {channel.Name} | {channel.Id}\n" +
                    $"`Channel Destroyed By:` {entry.User.Mention} | {entry.User.Id}\n" +
                    $"`Channel Destroyed At:` {DateTime.UtcNow:dd/MM/yyyy}\n" +
                    $"`Channel Type:` {createdType}");

            await logChannel.SendMessageAsync(embed: eb.Build());
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

        if (GuildLogSettings.TryGetValue(channel.Guild.Id, out var logSetting))
        {
            if (logSetting.ChannelUpdatedId is null or 0)
                return;

            var logChannel = channel.Guild.GetTextChannel(logSetting.ChannelUpdatedId.Value);

            if (logChannel is null)
                return;

            var audit = await channel.Guild.GetAuditLogsAsync(1, actionType: ActionType.ChannelUpdated).FlattenAsync();

            var entry = audit.FirstOrDefault();

            var eb = new EmbedBuilder()
                .WithOkColor();

            if (channel.Name != channel2.Name)
                eb.WithTitle("Channel Name Updated")
                    .WithDescription(
                        $"`Old Name:` {channel.Name}\n" +
                        $"`New Name:` {channel2.Name}\n" +
                        $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

            if (channel.Position != channel2.Position)
                eb.WithTitle("Channel Position Updated")
                    .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                     $"`Old Position:` {channel.Position}\n" +
                                     $"`New Position:` {channel2.Position}\n" +
                                     $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

            if (channel is SocketTextChannel textChannel && channel2 is SocketTextChannel textChannel2)
            {
                if (textChannel.Topic != textChannel2.Topic)
                    eb.WithTitle("Channel Topic Updated")
                        .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                         $"`Old Topic:` {textChannel.Topic}\n" +
                                         $"`New Topic:` {textChannel2.Topic}\n" +
                                         $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (textChannel.IsNsfw != textChannel2.IsNsfw)
                    eb.WithTitle("Channel NSFW Updated")
                        .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                         $"`Old NSFW:` {textChannel.IsNsfw}\n" +
                                         $"`New NSFW:` {textChannel2.IsNsfw}\n" +
                                         $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (textChannel.SlowModeInterval != textChannel2.SlowModeInterval)
                    eb.WithTitle("Channel Slowmode Interval Updated")
                        .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                         $"`Old Slowmode Interval:` {textChannel.SlowModeInterval}\n" +
                                         $"`New Slowmode Interval:` {textChannel2.SlowModeInterval}\n" +
                                         $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (textChannel.CategoryId != textChannel2.CategoryId)
                    eb.WithTitle("Channel Category Updated")
                        .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                         $"`Old Category:` {textChannel.Category}\n" +
                                         $"`New Category:` {textChannel2.Category}\n" +
                                         $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");
            }

            if (channel is SocketVoiceChannel voiceChannel && channel2 is SocketVoiceChannel voiceChannel2)
            {
                if (voiceChannel.Bitrate != voiceChannel2.Bitrate)
                    eb.WithTitle("Channel Bitrate Updated")
                        .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                         $"`Old Bitrate:` {voiceChannel.Bitrate}\n" +
                                         $"`New Bitrate:` {voiceChannel2.Bitrate}\n" +
                                         $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (voiceChannel.UserLimit != voiceChannel2.UserLimit)
                    eb.WithTitle("Channel User Limit Updated")
                        .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                         $"`Old User Limit:` {voiceChannel.UserLimit}\n" +
                                         $"`New User Limit:` {voiceChannel2.UserLimit}\n" +
                                         $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (voiceChannel.CategoryId != voiceChannel2.CategoryId)
                    eb.WithTitle("Channel Category Updated")
                        .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                         $"`Old Category:` {voiceChannel.Category}\n" +
                                         $"`New Category:` {voiceChannel2.Category}\n" +
                                         $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (voiceChannel.Position != voiceChannel2.Position)
                    eb.WithTitle("Channel Position Updated")
                        .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                         $"`Old Position:` {voiceChannel.Position}\n" +
                                         $"`New Position:` {voiceChannel2.Position}\n" +
                                         $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (voiceChannel.VideoQualityMode != voiceChannel2.VideoQualityMode)
                    eb.WithTitle("Channel Video Quality Mode Updated")
                        .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                         $"`Old Video Quality Mode:` {voiceChannel.VideoQualityMode}\n" +
                                         $"`New Video Quality Mode:` {voiceChannel2.VideoQualityMode}\n" +
                                         $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (voiceChannel.RTCRegion != voiceChannel2.RTCRegion)
                    eb.WithTitle("Channel RTC Region Updated")
                        .WithDescription($"`Channel:` {channel2.Name} | {channel2.Id}" +
                                         $"`Old RTC Region:` {voiceChannel.RTCRegion}\n" +
                                         $"`New RTC Region:` {voiceChannel2.RTCRegion}\n" +
                                         $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");
            }

            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when voice state is updated.
    /// </summary>
    /// <param name="args">The user that had their voice state updated.</param>
    /// <param name="args2">The voice state before the update.</param>
    /// <param name="args3">The voice state after the update.</param>
    private async Task OnVoicePresence(SocketUser args, SocketVoiceState args2, SocketVoiceState args3)
    {
        if (args.IsBot)
            return;

        if (args is not IGuildUser guildUser)
            return;

        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.LogVoicePresenceId is null or 0)
                return;

            var logChannel = await guildUser.Guild.GetTextChannelAsync(logSetting.LogVoicePresenceId.Value);

            if (logChannel is null)
                return;

            var eb = new EmbedBuilder();

            if (args2.VoiceChannel is not null && args3.VoiceChannel is not null)
                eb.WithTitle("User Moved Voice Channels")
                    .WithDescription(
                        $"`User:` {args.Mention} | {args.Id}\n" +
                        $"`Old Channel:` {args2.VoiceChannel.Name}\n" +
                        $"`New Channel:` {args3.VoiceChannel.Name}");

            if (args2.VoiceChannel is null && args3.VoiceChannel is not null)
                eb.WithTitle("User Joined Voice Channel")
                    .WithDescription(
                        $"`User:` {args.Mention} | {args.Id}\n" +
                        $"`Channel:` {args3.VoiceChannel.Name}");

            if (args2.VoiceChannel is not null && args3.VoiceChannel is null)
                eb.WithTitle("User Left Voice Channel")
                    .WithDescription(
                        $"`User:` {args.Mention} | {args.Id}\n" +
                        $"`Channel:` {args2.VoiceChannel.Name}");

            if (!args2.IsDeafened && args3.IsDeafened)
                eb.WithTitle($"User {(!args3.IsSelfDeafened ? "Server Voice Deafened" : "Self Voice Deafened")}")
                    .WithDescription(
                        $"`User:` {args.Mention} | {args.Id}\n" +
                        $"`Channel:` {args2.VoiceChannel.Name}");

            if (args2.IsDeafened && !args3.IsDeafened)
                eb.WithTitle("User UnDeafened")
                    .WithDescription(
                        $"`User:` {args.Mention} | {args.Id}\n" +
                        $"`Channel:` {args2.VoiceChannel.Name}");

            if (!args2.IsMuted && args3.IsMuted)
                eb.WithTitle($"User {(!args3.IsSelfMuted ? "Server Voice Muted" : "Self Voice Muted")}")
                    .WithDescription(
                        $"`User:` {args.Mention} | {args.Id}\n" +
                        $"`Channel:` {args2.VoiceChannel.Name}");

            if (args2.IsMuted && !args3.IsMuted)
                eb.WithTitle("User Voice UnMuted")
                    .WithDescription(
                        $"`User:` {args.Mention} | {args.Id}\n" +
                        $"`Channel:` {args2.VoiceChannel.Name}");


            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user says something using tts in a channel. Currently unimplemented.
    /// </summary>
    /// <param name="args">The user that used tts.</param>
    /// <param name="args2">The voice state before the update.</param>
    /// <param name="args3">The voice state after the update.</param>
    /// <exception cref="NotImplementedException"></exception>
    private async Task OnVoicePresenceTts(SocketUser args, SocketVoiceState args2, SocketVoiceState args3)
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Handles the event when a user is muted in a guild. Currently unimplemented.
    /// </summary>
    /// <param name="guildUser">The user that was muted.</param>
    /// <param name="args2">The user that muted the user.</param>
    /// <param name="args3">Type of mute. <see cref="MuteType" /></param>
    /// <param name="args4">The reason the user was muted.</param>
    /// <exception cref="NotImplementedException"></exception>
    private async Task OnUserMuted(IGuildUser guildUser, IUser args2, MuteType args3, string args4)
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Handles the event when a user is unmuted in a guild. Currently unimplemented.
    /// </summary>
    /// <param name="args">The user that was unmuted.</param>
    /// <param name="args2">The user that unmuted the user.</param>
    /// <param name="args3">Type of mute. <see cref="MuteType" /></param>
    /// <param name="args4">The reason the user was unmuted.</param>
    /// <exception cref="NotImplementedException"></exception>
    private async Task OnUserUnmuted(IGuildUser args, IUser args2, MuteType args3, string args4)
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Sets the log channel for a specific type of log.
    /// </summary>
    /// <param name="guildId">The guildId to set the log setting for.</param>
    /// <param name="channelId">The channelId to set the log channel to.</param>
    /// <param name="type">The type of log to set the channel for.</param>
    public async Task SetLogChannel(ulong guildId, ulong channelId, LogType type)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var logSetting = (await dbContext.LogSettingsFor(guildId)).LogSetting;
        switch (type)
        {
            case LogType.Other:
                logSetting.LogOtherId = channelId;
                break;
            case LogType.EventCreated:
                logSetting.EventCreatedId = channelId;
                break;
            case LogType.RoleUpdated:
                logSetting.RoleUpdatedId = channelId;
                break;
            case LogType.RoleCreated:
                logSetting.RoleCreatedId = channelId;
                break;
            case LogType.ServerUpdated:
                logSetting.ServerUpdatedId = channelId;
                break;
            case LogType.ThreadCreated:
                logSetting.ThreadCreatedId = channelId;
                break;
            case LogType.UserRoleAdded:
                logSetting.UserRoleAddedId = channelId;
                break;
            case LogType.UserRoleRemoved:
                logSetting.UserRoleRemovedId = channelId;
                break;
            case LogType.UsernameUpdated:
                logSetting.UsernameUpdatedId = channelId;
                break;
            case LogType.NicknameUpdated:
                logSetting.NicknameUpdatedId = channelId;
                break;
            case LogType.ThreadDeleted:
                logSetting.ThreadDeletedId = channelId;
                break;
            case LogType.ThreadUpdated:
                logSetting.ThreadUpdatedId = channelId;
                break;
            case LogType.MessageUpdated:
                logSetting.MessageUpdatedId = channelId;
                break;
            case LogType.MessageDeleted:
                logSetting.MessageDeletedId = channelId;
                break;
            case LogType.UserJoined:
                logSetting.UserJoinedId = channelId;
                break;
            case LogType.UserLeft:
                logSetting.UserLeftId = channelId;
                break;
            case LogType.UserBanned:
                logSetting.UserBannedId = channelId;
                break;
            case LogType.UserUnbanned:
                logSetting.UserUnbannedId = channelId;
                break;
            case LogType.UserUpdated:
                logSetting.UserUpdatedId = channelId;
                break;
            case LogType.ChannelCreated:
                logSetting.ChannelCreatedId = channelId;
                break;
            case LogType.ChannelDestroyed:
                logSetting.ChannelDestroyedId = channelId;
                break;
            case LogType.ChannelUpdated:
                logSetting.ChannelUpdatedId = channelId;
                break;
            case LogType.VoicePresence:
                logSetting.LogVoicePresenceId = channelId;
                break;
            case LogType.VoicePresenceTts:
                logSetting.LogVoicePresenceTTSId = channelId;
                break;
            case LogType.UserMuted:
                logSetting.UserMutedId = channelId;
                break;
        }

        try
        {
            dbContext.LogSettings.Update(logSetting);
            await dbContext.SaveChangesAsync();
            GuildLogSettings.AddOrUpdate(guildId, _ => logSetting, (_, _) => logSetting);
        }
        catch (Exception e)
        {
            Log.Error(e, "There was an issue setting log settings");
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
        await using var dbContext = await dbProvider.GetContextAsync();
        var logSetting = (await dbContext.LogSettingsFor(guildId)).LogSetting;
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
                logSetting.LogVoicePresenceTTSId = channelId;
                break;
            case LogCategoryTypes.Users:
                logSetting.NicknameUpdatedId = channelId;
                logSetting.AvatarUpdatedId = channelId;
                logSetting.UsernameUpdatedId = channelId;
                logSetting.UserRoleAddedId = channelId;
                logSetting.UserRoleRemovedId = channelId;
                logSetting.LogVoicePresenceId = channelId;
                break;
            case LogCategoryTypes.Threads:
                logSetting.ThreadCreatedId = channelId;
                logSetting.ThreadDeletedId = channelId;
                logSetting.ThreadUpdatedId = channelId;
                break;
            case LogCategoryTypes.Roles:
                logSetting.RoleCreatedId = channelId;
                logSetting.RoleDeletedId = channelId;
                logSetting.RoleUpdatedId = channelId;
                break;
            case LogCategoryTypes.Server:
                logSetting.ServerUpdatedId = channelId;
                logSetting.EventCreatedId = channelId;
                break;
            case LogCategoryTypes.Channel:
                logSetting.ChannelUpdatedId = channelId;
                logSetting.ChannelCreatedId = channelId;
                logSetting.ChannelDestroyedId = channelId;
                break;
            case LogCategoryTypes.Messages:
                logSetting.MessageDeletedId = channelId;
                logSetting.MessageUpdatedId = channelId;
                break;
            case LogCategoryTypes.Moderation:
                logSetting.UserMutedId = channelId;
                logSetting.UserBannedId = channelId;
                logSetting.UserUnbannedId = channelId;
                break;
            case LogCategoryTypes.None:
                logSetting.AvatarUpdatedId = 0;
                logSetting.ChannelCreatedId = 0;
                logSetting.ChannelDestroyedId = 0;
                logSetting.ChannelUpdatedId = 0;
                logSetting.EventCreatedId = 0;
                logSetting.LogOtherId = 0;
                logSetting.MessageDeletedId = 0;
                logSetting.MessageUpdatedId = 0;
                logSetting.NicknameUpdatedId = 0;
                logSetting.RoleCreatedId = 0;
                logSetting.RoleDeletedId = 0;
                logSetting.RoleUpdatedId = 0;
                logSetting.ServerUpdatedId = 0;
                logSetting.ThreadCreatedId = 0;
                logSetting.ThreadDeletedId = 0;
                logSetting.ThreadUpdatedId = 0;
                logSetting.UserBannedId = 0;
                logSetting.UserJoinedId = 0;
                logSetting.UserLeftId = 0;
                logSetting.UserMutedId = 0;
                logSetting.UsernameUpdatedId = 0;
                logSetting.UserUnbannedId = 0;
                logSetting.UserUpdatedId = 0;
                logSetting.LogUserPresenceId = 0;
                logSetting.LogVoicePresenceId = 0;
                logSetting.UserRoleAddedId = 0;
                logSetting.UserRoleRemovedId = 0;
                logSetting.LogVoicePresenceTTSId = 0;
                break;
        }

        await dbContext.SaveChangesAsync();
        GuildLogSettings.AddOrUpdate(guildId, _ => logSetting, (_, _) => logSetting);
    }
}