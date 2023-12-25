using Discord.Rest;
using Mewdeko.Modules.Moderation.Services;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Administration.Services;

public class NewLogCommandService : INService
{
    private readonly DbService db;
    private readonly IDataCache cache;
    private readonly DiscordSocketClient client;
    public ConcurrentDictionary<ulong, LogSetting> GuildLogSettings { get; }

    public enum LogType
    {
        Other,
        EventCreated,
        RoleUpdated,
        RoleCreated,
        RoleDeleted,
        ServerUpdated,
        ThreadCreated,
        UserRoleAdded,
        UserRoleRemoved,
        UsernameUpdated,
        NicknameUpdated,
        ThreadDeleted,
        ThreadUpdated,
        MessageUpdated,
        MessageDeleted,
        UserJoined,
        UserLeft,
        UserBanned,
        UserUnbanned,
        UserUpdated,
        ChannelCreated,
        ChannelDestroyed,
        ChannelUpdated,
        VoicePresence,
        VoicePresenceTts,
        UserMuted
    }

    public enum LogCategoryTypes
    {
        All,
        Users,
        Threads,
        Roles,
        Server,
        Channel,
        Messages,
        Moderation,
        None
    }


    public NewLogCommandService(DbService db, IDataCache cache, DiscordSocketClient client, EventHandler handler,
        MuteService muteService)
    {
        this.db = db;
        this.cache = cache;
        this.client = client;
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

        using var uow = db.GetDbContext();
        var guildIds = client.Guilds.Select(x => x.Id).ToList();
        var configs = uow.GuildConfigs
            .AsQueryable()
            .Include(gc => gc.LogSetting)
            .ThenInclude(ls => ls.IgnoredChannels)
            .Where(x => guildIds.Contains(x.GuildId))
            .ToList();

        GuildLogSettings = configs
            .ToDictionary(g => g.GuildId, g => g.LogSetting)
            .ToConcurrent();
    }

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

            var auditLogs = await args.Guild.GetAuditLogsAsync(1, actionType: ActionType.GuildUpdated).FlattenAsync();


            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Role Deleted")
                .WithDescription($"`Role:` {args.Name}\n" +
                                 $"`Deleted By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}\n" +
                                 $"`Deleted At:` {DateTime.UtcNow}\n" +
                                 $"`Members:` {args.Members.Count()}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

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
                    .WithDescription($"`New Role Color:` {arsg2.Color}\n" +
                                     $"`Old Role Color:` {args.Color}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.IsHoisted != arsg2.IsHoisted)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Property Hoisted Updated")
                    .WithDescription($"`New Role Hoisted:` {arsg2.IsHoisted}\n" +
                                     $"`Old Role Hoisted:` {args.IsHoisted}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.IsMentionable != arsg2.IsMentionable)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Property Mentionable Updated")
                    .WithDescription($"`New Role Mentionable:` {arsg2.IsMentionable}\n" +
                                     $"`Old Role Mentionable:` {args.IsMentionable}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.IsManaged != arsg2.IsManaged)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Property Managed Updated")
                    .WithDescription($"`New Role Managed:` {arsg2.IsManaged}\n" +
                                     $"`Old Role Managed:` {args.IsManaged}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.Position != arsg2.Position)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Position Updated")
                    .WithDescription($"`New Role Position:` {arsg2.Position}\n" +
                                     $"`Old Role Position:` {args.Position}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (!arsg2.Permissions.Equals(args.Permissions))
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Permissions Updated")
                    .WithDescription($"`New Role Permissions:` {arsg2.Permissions}\n" +
                                     $"`Old Role Permissions:` {args.Permissions}\n" +
                                     $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");
            }

            if (args.Icon != arsg2.Icon)
            {
                eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Role Icon Updated")
                    .WithDescription(
                        $"`Updated By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}")
                    .WithThumbnailUrl(args.GetIconUrl())
                    .WithImageUrl(arsg2.GetIconUrl());
            }

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    private async Task OnEventCreated(SocketGuildEvent args)
    {
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            if (logSetting.EventCreatedId is null or 0)
                return;

            var channel = args.Guild.GetTextChannel(logSetting.EventCreatedId.Value);

            if (channel is null)
                return;

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

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

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

    private async Task OnUserRoleAdded(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser arsg2)
    {
        if (GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting))
        {
            if (!cacheable.HasValue) return;
            if (logSetting.UserRoleAddedId is null or 0)
                return;

            var channel = arsg2.Guild.GetTextChannel(logSetting.UserRoleAddedId.Value);

            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await arsg2.Guild.GetAuditLogsAsync(1, actionType: ActionType.MemberRoleUpdated)
                .FlattenAsync();

            var added = arsg2.Roles.Except(cacheable.Value.Roles);
            if (!added.Any())
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("User Role(s) Added")
                .WithDescription($"`Role(s):` {string.Join(",", added.Select(x => $"{x.Mention}"))}\n" +
                                 $"`Added By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    private async Task OnUserRoleRemoved(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser arsg2)
    {
        if (GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting))
        {
            if (!cacheable.HasValue) return;
            if (logSetting.UserRoleRemovedId is null or 0)
                return;

            var channel = arsg2.Guild.GetTextChannel(logSetting.UserRoleRemovedId.Value);

            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await arsg2.Guild.GetAuditLogsAsync(1, actionType: ActionType.MemberRoleUpdated)
                .FlattenAsync();

            var removed = arsg2.Roles.Except(cacheable.Value.Roles);
            if (!removed.Any()) return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("User Role(s) Removed")
                .WithDescription($"`Role(s):` {string.Join(",", removed.Select(x => $"{x.Mention}"))}\n" +
                                 $"`Added By:` {auditLogs.FirstOrDefault().User.Mention} | {auditLogs.FirstOrDefault().User.Id}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

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

    private async Task OnMessageUpdated(Cacheable<IMessage, ulong> cacheable, SocketMessage args2,
        ISocketMessageChannel args3)
    {
        if (!cacheable.HasValue) return;
        var oldMessage = cacheable.Value;
        if (args3 is not SocketTextChannel guildChannel)
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

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Message Deleted")
                .WithDescription(
                    $"`Message Author:` {message.Author.Mention}\n" +
                    $"`Message Channel:` {guildChannel.Mention} | {guildChannel.Id}\n" +
                    $"`Message Content:` {message.Content}\n" +
                    $"`Deleted By:` {(entry is null ? message.Author.Mention : entry.User.Mention)} | {(entry is null ? message.Author.Id : entry.User.Id)}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

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
                    $"`User Status:` {guildUser.Status}" +
                    $"`User Id:` {guildUser.Id}" +
                    $"`User Global Name:` {guildUser.GlobalName ?? guildUser.Username}")
                .WithThumbnailUrl(guildUser.RealAvatarUrl().ToString());

            var component = new ComponentBuilder()
                .WithButton("View User", style: ButtonStyle.Link, url: $"discord://-/users/{guildUser.Id}").Build();

            await channel.SendMessageAsync(components: component, embed: eb.Build());
        }
    }

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
                    $"`User Id:` {usr.Id}" +
                    $"`User Global Name:` {arsg2.GlobalName ?? arsg2.Username}" +
                    $"`Account Created:` {usr.CreatedAt:dd/MM/yyyy}\n" +
                    $"`Joined Server:` {usr.JoinedAt:dd/MM/yyyy}\n" +
                    $"`Roles:` {string.Join(", ", usr.Roles.Select(x => x.Mention))}")
                .WithThumbnailUrl(arsg2.RealAvatarUrl().ToString());

            var component = new ComponentBuilder().WithButton("View User (May not work)", style: ButtonStyle.Link,
                url: $"discord://-/users/{arsg2.Id}").Build();

            await channel.SendMessageAsync(components: component, embed: eb.Build());
        }
    }

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
                    $"`User Id:` {args.Id}" +
                    $"`User Global Name:` {args.GlobalName ?? args.Username}" +
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
                        $"`User Id:` {args.Id}" +
                        $"`User Global Name:` {args.GlobalName ?? args.Username}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

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
                    $"`Channel Created By:` {entry.User.Mention} | {entry.User.Id}" +
                    $"`Channel Created At:` {channel.CreatedAt:dd/MM/yyyy}\n" +
                    $"`Channel Type:` {createdType}");

            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

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
                    $"`Channel Destroyed By:` {entry.User.Mention} | {entry.User.Id}" +
                    $"`Channel Destroyed At:` {DateTime.UtcNow:dd/MM/yyyy}\n" +
                    $"`Channel Type:` {createdType}");

            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

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
                    .WithDescription(
                        $"`Old Position:` {channel.Position}\n" +
                        $"`New Position:` {channel2.Position}\n" +
                        $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

            if (channel is SocketTextChannel textChannel && channel2 is SocketTextChannel textChannel2)
            {
                if (textChannel.Topic != textChannel2.Topic)
                    eb.WithTitle("Channel Topic Updated")
                        .WithDescription(
                            $"`Old Topic:` {textChannel.Topic}\n" +
                            $"`New Topic:` {textChannel2.Topic}\n" +
                            $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (textChannel.IsNsfw != textChannel2.IsNsfw)
                    eb.WithTitle("Channel NSFW Updated")
                        .WithDescription(
                            $"`Old NSFW:` {textChannel.IsNsfw}\n" +
                            $"`New NSFW:` {textChannel2.IsNsfw}\n" +
                            $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (textChannel.SlowModeInterval != textChannel2.SlowModeInterval)
                    eb.WithTitle("Channel Slowmode Interval Updated")
                        .WithDescription(
                            $"`Old Slowmode Interval:` {textChannel.SlowModeInterval}\n" +
                            $"`New Slowmode Interval:` {textChannel2.SlowModeInterval}\n" +
                            $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (textChannel.CategoryId != textChannel2.CategoryId)
                    eb.WithTitle("Channel Category Updated")
                        .WithDescription(
                            $"`Old Category:` {textChannel.Category}\n" +
                            $"`New Category:` {textChannel2.Category}\n" +
                            $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");
            }

            if (channel is SocketVoiceChannel voiceChannel && channel2 is SocketVoiceChannel voiceChannel2)
            {
                if (voiceChannel.Bitrate != voiceChannel2.Bitrate)
                    eb.WithTitle("Channel Bitrate Updated")
                        .WithDescription(
                            $"`Old Bitrate:` {voiceChannel.Bitrate}\n" +
                            $"`New Bitrate:` {voiceChannel2.Bitrate}\n" +
                            $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (voiceChannel.UserLimit != voiceChannel2.UserLimit)
                    eb.WithTitle("Channel User Limit Updated")
                        .WithDescription(
                            $"`Old User Limit:` {voiceChannel.UserLimit}\n" +
                            $"`New User Limit:` {voiceChannel2.UserLimit}\n" +
                            $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (voiceChannel.CategoryId != voiceChannel2.CategoryId)
                    eb.WithTitle("Channel Category Updated")
                        .WithDescription(
                            $"`Old Category:` {voiceChannel.Category}\n" +
                            $"`New Category:` {voiceChannel2.Category}\n" +
                            $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (voiceChannel.Position != voiceChannel2.Position)
                    eb.WithTitle("Channel Position Updated")
                        .WithDescription(
                            $"`Old Position:` {voiceChannel.Position}\n" +
                            $"`New Position:` {voiceChannel2.Position}\n" +
                            $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (voiceChannel.VideoQualityMode != voiceChannel2.VideoQualityMode)
                    eb.WithTitle("Channel Video Quality Mode Updated")
                        .WithDescription(
                            $"`Old Video Quality Mode:` {voiceChannel.VideoQualityMode}\n" +
                            $"`New Video Quality Mode:` {voiceChannel2.VideoQualityMode}\n" +
                            $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");

                if (voiceChannel.RTCRegion != voiceChannel2.RTCRegion)
                    eb.WithTitle("Channel RTC Region Updated")
                        .WithDescription(
                            $"`Old RTC Region:` {voiceChannel.RTCRegion}\n" +
                            $"`New RTC Region:` {voiceChannel2.RTCRegion}\n" +
                            $"`Channel Updated By:` {entry.User.Mention} | {entry.User.Id}");
            }

            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

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

    private async Task OnVoicePresenceTts(SocketUser args, SocketVoiceState args2, SocketVoiceState args3)
    {
        throw new NotImplementedException();
    }

    private async Task OnUserMuted(IGuildUser guildUser, IUser args2, MuteType args3, string args4)
    {
        throw new NotImplementedException();
    }

    private async Task OnUserUnmuted(IGuildUser args, IUser args2, MuteType args3, string args4)
    {
        throw new NotImplementedException();
    }

    public async Task SetLogChannel(ulong guildId, ulong channelId, LogType type)
    {
        await using var uow = db.GetDbContext();
        var logSetting = (await uow.LogSettingsFor(guildId)).LogSetting;
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

        uow.LogSettings.Update(logSetting);
        await uow.SaveChangesAsync();
        GuildLogSettings.AddOrUpdate(guildId, _ => logSetting, (_, _) => logSetting);
    }

    public async Task LogSetByType(ulong guildId, ulong channelId, LogCategoryTypes categoryTypes)
    {
        await using var uow = db.GetDbContext();
        var logSetting = (await uow.LogSettingsFor(guildId)).LogSetting;
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

        await uow.SaveChangesAsync();
        GuildLogSettings.AddOrUpdate(guildId, _ => logSetting, (_, _) => logSetting);
    }
}