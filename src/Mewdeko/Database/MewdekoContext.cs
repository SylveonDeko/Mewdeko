using Mewdeko.Services.Impl;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database;

/// <summary>
///     Represents the database context for Mewdeko.
/// </summary>
public class MewdekoContext : DbContext
{
    private readonly BotCredentials creds;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MewdekoContext" /> class.
    /// </summary>
    /// <param name="options">The options to be used by the DbContext.</param>
    public MewdekoContext(DbContextOptions options) : base(options)
    {
        creds = new BotCredentials();
    }

    /// <summary>
    ///     Gets or sets the global user balances.
    /// </summary>
    public DbSet<GlobalUserBalance> GlobalUserBalances { get; set; }

    /// <summary>
    ///     Gets or sets invite counts
    /// </summary>
    public DbSet<InviteCount> InviteCounts { get; set; }

    /// <summary>
    ///     Gets or sets invited by
    /// </summary>
    public DbSet<InvitedBy> InvitedBy { get; set; }

    /// <summary>
    ///     Gets or sets the lockdown channel permissions.
    /// </summary>
    public DbSet<LockdownChannelPermissions> LockdownChannelPermissions { get; set; }

    /// <summary>
    ///     Gets or sets the giveaway users.
    /// </summary>
    public DbSet<GiveawayUsers> GiveawayUsers { get; set; }

    /// <summary>
    ///     Role Monitor
    /// </summary>
    public DbSet<RoleMonitoringSettings> RoleMonitoringSettings { get; set; }

    /// <summary>
    ///     Role Monitor BLR
    /// </summary>
    public DbSet<BlacklistedRole> BlacklistedRoles { get; set; }

    /// <summary>
    ///     Role Monitor BLP
    /// </summary>
    public DbSet<BlacklistedPermission> BlacklistedPermissions { get; set; }

    /// <summary>
    ///     Role Monitor WR
    /// </summary>
    public DbSet<WhitelistedRole> WhitelistedRoles { get; set; }

    /// <summary>
    ///     Role Monitor WU
    /// </summary>
    public DbSet<WhitelistedUser> WhitelistedUsers { get; set; }

    /// <summary>
    ///     The bots reviews, can be added via dashboard or the bot itself
    /// </summary>
    public DbSet<BotReviews> BotReviews { get; set; }

    /// <summary>
    ///     Message Counts
    /// </summary>
    public DbSet<MessageCount> MessageCounts { get; set; }

    /// <summary>
    ///     Gets or sets the anti-alt settings.
    /// </summary>
    public DbSet<AntiAltSetting> AntiAltSettings { get; set; }

    /// <summary>
    ///     Gets or sets the anti-spam settings.
    /// </summary>
    public DbSet<AntiSpamSetting> AntiSpamSettings { get; set; }

    /// <summary>
    ///     Gets or sets the anti-raid settings.
    /// </summary>
    public DbSet<AntiRaidSetting> AntiRaidSettings { get; set; }

    /// <summary>
    ///     Gets or sets ticket panels
    /// </summary>
    public DbSet<TicketPanel> TicketPanels { get; set; }

    /// <summary>
    ///     Gets or sets ticket buttons
    /// </summary>
    public DbSet<TicketButton> TicketButtons { get; set; }

    /// <summary>
    ///     Gets or sets the anti-spam ignore settings.
    /// </summary>
    public DbSet<AntiSpamIgnore> AntiSpamIgnore { get; set; }

    /// <summary>
    ///     Gets or sets the guild user balances.
    /// </summary>
    public DbSet<GuildUserBalance> GuildUserBalances { get; set; }

    /// <summary>
    ///     Gets or sets the transaction histories.
    /// </summary>
    public DbSet<TransactionHistory> TransactionHistories { get; set; }

    /// <summary>
    ///     Gets or sets the auto publish settings.
    /// </summary>
    public DbSet<AutoPublish> AutoPublish { get; set; }

    /// <summary>
    ///     Gets or sets the auto ban roles.
    /// </summary>
    public DbSet<AutoBanRoles> AutoBanRoles { get; set; }

    /// <summary>
    ///     Logging settings for guilds
    /// </summary>
    public DbSet<LoggingV2> LoggingV2 { get; set; }

    /// <summary>
    ///     Gets or sets the publish word blacklists.
    /// </summary>
    public DbSet<PublishWordBlacklist> PublishWordBlacklists { get; set; }

    /// <summary>
    ///     Gets or sets the publish user blacklists.
    /// </summary>
    public DbSet<PublishUserBlacklist> PublishUserBlacklists { get; set; }

    /// <summary>
    ///     Gets or sets the join and leave logs.
    /// </summary>
    public DbSet<JoinLeaveLogs> JoinLeaveLogs { get; set; }

    /// <summary>
    ///     Gets or sets the guild configurations.
    /// </summary>
    public DbSet<GuildConfig> GuildConfigs { get; set; }

    /// <summary>
    ///     Gets or sets the suggestions.
    /// </summary>
    public DbSet<SuggestionsModel> Suggestions { get; set; }

    /// <summary>
    ///     Gets or sets the filtered words.
    /// </summary>
    public DbSet<FilteredWord> FilteredWords { get; set; }

    /// <summary>
    ///     Gets or sets the owner only settings.
    /// </summary>
    public DbSet<OwnerOnly> OwnerOnly { get; set; }

    /// <summary>
    ///     Gets or sets the warnings (second version).
    /// </summary>
    public DbSet<Warning2> Warnings2 { get; set; }

    /// <summary>
    ///     Gets or sets the templates.
    /// </summary>
    public DbSet<Template> Templates { get; set; }

    /// <summary>
    ///     Gets or sets the server recovery store.
    /// </summary>
    public DbSet<ServerRecoveryStore> ServerRecoveryStore { get; set; }

    /// <summary>
    ///     Gets or sets the AFK settings.
    /// </summary>
    public DbSet<Afk> Afk { get; set; }

    /// <summary>
    ///     Gets or sets the multi greet settings.
    /// </summary>
    public DbSet<MultiGreet> MultiGreets { get; set; }

    /// <summary>
    ///     Gets or sets the user role states.
    /// </summary>
    public DbSet<UserRoleStates> UserRoleStates { get; set; }

    /// <summary>
    ///     Gets or sets the role state settings.
    /// </summary>
    public DbSet<RoleStateSettings> RoleStateSettings { get; set; }

    /// <summary>
    ///     Gets or sets the giveaways.
    /// </summary>
    public DbSet<Giveaways> Giveaways { get; set; }

    /// <summary>
    ///     Gets or sets the starboard posts.
    /// </summary>
    public DbSet<StarboardPosts> Starboard { get; set; }

    /// <summary>
    ///     Gets or sets the quotes.
    /// </summary>
    public DbSet<Quote> Quotes { get; set; }

    /// <summary>
    ///     Gets or sets the reminders.
    /// </summary>
    public DbSet<Reminder> Reminders { get; set; }

    /// <summary>
    ///     Gets or sets the confessions.
    /// </summary>
    public DbSet<Confessions> Confessions { get; set; }

    /// <summary>
    ///     Gets or sets the self-assigned roles.
    /// </summary>
    public DbSet<SelfAssignedRole> SelfAssignableRoles { get; set; }

    /// <summary>
    ///     Gets or sets the role greets.
    /// </summary>
    public DbSet<RoleGreet> RoleGreets { get; set; }

    /// <summary>
    ///     Gets or sets the highlights.
    /// </summary>
    public DbSet<Highlights> Highlights { get; set; }

    /// <summary>
    ///     Gets or sets the command statistics.
    /// </summary>
    public DbSet<CommandStats> CommandStats { get; set; }

    /// <summary>
    ///     Gets or sets the highlight settings.
    /// </summary>
    public DbSet<HighlightSettings> HighlightSettings { get; set; }

    /// <summary>
    ///     Gets or sets the music playlists.
    /// </summary>
    public DbSet<MusicPlaylist> MusicPlaylists { get; set; }

    /// <summary>
    ///     Gets or sets the chat triggers.
    /// </summary>
    public DbSet<ChatTriggers> ChatTriggers { get; set; }

    /// <summary>
    ///     Gets or sets the music player settings.
    /// </summary>
    public DbSet<MusicPlayerSettings> MusicPlayerSettings { get; set; }

    /// <summary>
    ///     Gets or sets the warnings.
    /// </summary>
    public DbSet<Warning> Warnings { get; set; }

    /// <summary>
    ///     Gets or sets the user XP stats.
    /// </summary>
    public DbSet<UserXpStats> UserXpStats { get; set; }

    /// <summary>
    ///     Gets or sets the vote roles.
    /// </summary>
    public DbSet<VoteRoles> VoteRoles { get; set; }

    /// <summary>
    ///     Gets or sets the polls.
    /// </summary>
    public DbSet<Polls> Poll { get; set; }

    /// <summary>
    ///     Gets or sets the command cooldowns.
    /// </summary>
    public DbSet<CommandCooldown> CommandCooldown { get; set; }

    /// <summary>
    ///     Gets or sets the suggest votes.
    /// </summary>
    public DbSet<SuggestVotes> SuggestVotes { get; set; }

    /// <summary>
    ///     Gets or sets the suggest threads.
    /// </summary>
    public DbSet<SuggestThreads> SuggestThreads { get; set; }

    /// <summary>
    ///     Gets or sets the votes.
    /// </summary>
    public DbSet<Votes> Votes { get; set; }

    /// <summary>
    ///     Gets or sets the command aliases.
    /// </summary>
    public DbSet<CommandAlias> CommandAliases { get; set; }

    /// <summary>
    ///     Gets or sets the ignored log channels.
    /// </summary>
    public DbSet<IgnoredLogChannel> IgnoredLogChannels { get; set; }

    /// <summary>
    ///     Gets or sets the rotating playing statuses.
    /// </summary>
    public DbSet<RotatingPlayingStatus> RotatingStatus { get; set; }

    /// <summary>
    ///     Gets or sets the blacklist entries.
    /// </summary>
    public DbSet<BlacklistEntry> Blacklist { get; set; }

    /// <summary>
    ///     Gets or sets the auto commands.
    /// </summary>
    public DbSet<AutoCommand> AutoCommands { get; set; }

    /// <summary>
    ///     Gets or sets the auto ban words.
    /// </summary>
    public DbSet<AutoBanEntry> AutoBanWords { get; set; }

    /// <summary>
    ///     Gets or sets the status roles.
    /// </summary>
    public DbSet<StatusRolesTable> StatusRoles { get; set; }

    /// <summary>
    ///     Gets or sets the ban templates.
    /// </summary>
    public DbSet<BanTemplate> BanTemplates { get; set; }

    /// <summary>
    ///     Gets or sets the discord permission overrides.
    /// </summary>
    public DbSet<DiscordPermOverride> DiscordPermOverrides { get; set; }

    /// <summary>
    ///     Gets or sets the discord users.
    /// </summary>
    public DbSet<DiscordUser> DiscordUser { get; set; }

    /// <summary>
    ///     Gets or sets the feed subscriptions.
    /// </summary>
    public DbSet<FeedSub> FeedSubs { get; set; }

    /// <summary>
    ///     Gets or sets the muted user IDs.
    /// </summary>
    public DbSet<MutedUserId> MutedUserIds { get; set; }

    /// <summary>
    ///     Gets or sets the playlist songs.
    /// </summary>
    public DbSet<PlaylistSong> PlaylistSongs { get; set; }

    /// <summary>
    ///     Gets or sets the poll votes.
    /// </summary>
    public DbSet<PollVote> PollVotes { get; set; }

    /// <summary>
    ///     Gets or sets the role connection authentication storage.
    /// </summary>
    public DbSet<RoleConnectionAuthStorage> AuthCodes { get; set; }

    /// <summary>
    ///     Gets or sets the repeaters.
    /// </summary>
    public DbSet<Repeater> Repeaters { get; set; }

    /// <summary>
    ///     Gets or sets the unban timers.
    /// </summary>
    public DbSet<UnbanTimer> UnbanTimers { get; set; }

    /// <summary>
    ///     Gets or sets the unmute timers.
    /// </summary>
    public DbSet<UnmuteTimer> UnmuteTimers { get; set; }

    /// <summary>
    ///     Gets or sets the unrole timers.
    /// </summary>
    public DbSet<UnroleTimer> UnroleTimers { get; set; }

    /// <summary>
    ///     Gets or sets the voice channel role information.
    /// </summary>
    public DbSet<VcRoleInfo> VcRoleInfos { get; set; }

    /// <summary>
    ///     Gets or sets the warning punishments.
    /// </summary>
    public DbSet<WarningPunishment> WarningPunishments { get; set; }

    /// <summary>
    ///     Gets or sets the second version of warning punishments.
    /// </summary>
    public DbSet<WarningPunishment2> WarningPunishments2 { get; set; }

    /// <summary>
    ///     gets or sets the local running instances, for dashboard management.
    /// </summary>
    public DbSet<LocalBotInstances> BotInstances { get; set; }

    /// <summary>
    ///     Settings for invite counting
    /// </summary>
    public DbSet<InviteCountSettings> InviteCountSettings { get; set; }

    /// <summary>
    ///     Configures the model that was discovered by convention from the entity types
    ///     exposed in <see cref="DbSet{TEntity}" /> properties on your derived context.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (creds.MigrateToPsql)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var newProperties = entityType.ClrType.GetProperties()
                    .Where(p => Attribute.IsDefined(p, typeof(NewPropertyAttribute)))
                    .ToList();

                foreach (var prop in newProperties)
                {
                    modelBuilder.Entity(entityType.ClrType).Ignore(prop.Name);
                }
            }
        }

        #region Afk

        var afkEntity = modelBuilder.Entity<Afk>();
        afkEntity.HasKey(x => x.Id);
        afkEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        afkEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        afkEntity.Property(x => x.WasTimed).HasDefaultValue(false);
        afkEntity.Property(x => x.Message).HasColumnType("text").IsRequired(false);
        afkEntity.Property(x => x.When).HasColumnType("timestamp without time zone").IsRequired(false);
        afkEntity.HasIndex(x => new
        {
            x.GuildId, x.UserId
        });

        #endregion

        #region AntiRaidSetting

        var antiRaidSettingEntity = modelBuilder.Entity<AntiRaidSetting>();
        antiRaidSettingEntity.HasKey(x => x.Id);
        antiRaidSettingEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        antiRaidSettingEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        antiRaidSettingEntity.Property(x => x.UserThreshold).HasColumnType("integer");
        antiRaidSettingEntity.Property(x => x.Seconds).HasColumnType("integer");
        antiRaidSettingEntity.Property(x => x.PunishDuration).HasColumnType("integer");
        antiRaidSettingEntity.Property(x => x.Action).HasConversion<int>();

        #endregion

        #region AntiSpamSetting

        var antiSpamSettingEntity = modelBuilder.Entity<AntiSpamSetting>();
        antiSpamSettingEntity.HasKey(x => x.Id);
        antiSpamSettingEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        antiSpamSettingEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        antiSpamSettingEntity.Property(x => x.MessageThreshold).HasColumnType("integer");
        antiSpamSettingEntity.Property(x => x.MuteTime).HasColumnType("integer");
        antiSpamSettingEntity.Property(x => x.RoleId).HasColumnType("numeric(20, 0)").IsRequired(false);
        antiSpamSettingEntity.Property(x => x.Action).HasConversion<int>();

        #endregion

        #region AntiMassMentionSetting

        var antiMassMentionEntity = modelBuilder.Entity<AntiMassMentionSetting>();
        antiMassMentionEntity.HasKey(x => x.Id);
        antiMassMentionEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        antiMassMentionEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        antiMassMentionEntity.Property(x => x.MentionThreshold).HasColumnType("integer");
        antiMassMentionEntity.Property(x => x.MaxMentionsInTimeWindow).HasColumnType("integer");
        antiMassMentionEntity.Property(x => x.TimeWindowSeconds).HasColumnType("integer");
        antiMassMentionEntity.Property(x => x.MuteTime).HasColumnType("integer");
        antiMassMentionEntity.Property(x => x.RoleId).HasColumnType("numeric(20, 0)").IsRequired(false);
        antiMassMentionEntity.Property(x => x.IgnoreBots).HasDefaultValue(true);
        antiMassMentionEntity.Property(x => x.Action).HasConversion<int>();

        #endregion

        #region AntiAltSetting

        var antiAltSettingEntity = modelBuilder.Entity<AntiAltSetting>();
        antiAltSettingEntity.HasKey(x => x.Id);
        antiAltSettingEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        antiAltSettingEntity.Property(x => x.MinAge).HasColumnType("text").IsRequired(false);
        antiAltSettingEntity.Property(x => x.Action).HasConversion<int>();
        antiAltSettingEntity.Property(x => x.ActionDurationMinutes).HasColumnType("integer");
        antiAltSettingEntity.Property(x => x.RoleId).HasColumnType("numeric(20, 0)").IsRequired(false);

        #endregion

        #region AutoBanRoles

        var autoBanRolesEntity = modelBuilder.Entity<AutoBanRoles>();
        autoBanRolesEntity.HasKey(x => x.Id);
        autoBanRolesEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        autoBanRolesEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        autoBanRolesEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)");
        autoBanRolesEntity.Property(x => x.RoleId).HasColumnType("numeric(20, 0)");

        #endregion

        #region AutoCommand

        var autoCommandEntity = modelBuilder.Entity<AutoCommand>();
        autoCommandEntity.HasKey(x => x.Id);
        autoCommandEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        autoCommandEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        autoCommandEntity.Property(x => x.CommandText).HasColumnType("text").IsRequired(false);
        autoCommandEntity.Property(x => x.ChannelId).HasColumnType("numeric(20, 0)");
        autoCommandEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired(false);
        autoCommandEntity.Property(x => x.VoiceChannelId).HasColumnType("numeric(20, 0)").IsRequired(false);
        autoCommandEntity.Property(x => x.Interval).HasColumnType("integer");

        #endregion

        #region BotReviews

        var botReviewsEntity = modelBuilder.Entity<BotReviews>();
        botReviewsEntity.HasKey(x => x.Id);
        botReviewsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        botReviewsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        botReviewsEntity.Property(x => x.Username).HasColumnType("text");
        botReviewsEntity.Property(x => x.AvatarUrl).HasColumnType("text").HasDefaultValue(string.Empty);
        botReviewsEntity.Property(x => x.Stars).HasColumnType("integer");

        #endregion

        #region CommandCooldown

        var commandCooldownEntity = modelBuilder.Entity<CommandCooldown>();
        commandCooldownEntity.HasKey(x => x.Id);
        commandCooldownEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        commandCooldownEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        commandCooldownEntity.Property(x => x.CommandName).HasColumnType("text").IsRequired(false);

        #endregion

        #region CommandStats

        var commandStatsEntity = modelBuilder.Entity<CommandStats>();
        commandStatsEntity.HasKey(x => x.Id);
        commandStatsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        commandStatsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        commandStatsEntity.Property(x => x.NameOrId).HasColumnType("text").HasDefaultValue("");
        commandStatsEntity.Property(x => x.Module).HasColumnType("text").HasDefaultValue("");
        commandStatsEntity.Property(x => x.IsSlash).HasDefaultValue(false);
        commandStatsEntity.Property(x => x.Trigger).HasDefaultValue(false);
        commandStatsEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)");
        commandStatsEntity.Property(x => x.ChannelId).HasColumnType("numeric(20, 0)");
        commandStatsEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)");

        #endregion

        #region DiscordUser

        var discordUserEntity = modelBuilder.Entity<DiscordUser>();
        discordUserEntity.HasKey(x => x.Id);
        discordUserEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        discordUserEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        discordUserEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)");
        discordUserEntity.Property(x => x.Username).HasColumnType("text").IsRequired(false);
        discordUserEntity.Property(x => x.Discriminator).HasColumnType("text").IsRequired(false);
        discordUserEntity.Property(x => x.AvatarId).HasColumnType("text").IsRequired(false);
        discordUserEntity.Property(x => x.TotalXp).HasColumnType("integer");
        discordUserEntity.Property(x => x.LastLevelUp).HasColumnType("timestamp without time zone").IsRequired(false);
        discordUserEntity.Property(x => x.Birthday).HasColumnType("timestamp without time zone").IsRequired(false);
        discordUserEntity.Property(x => x.ProfilePrivacy).HasConversion<int>();

        #endregion

        #region Highlights

        var highlightEntity = modelBuilder.Entity<Highlights>();
        highlightEntity.HasKey(x => x.Id);
        highlightEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        highlightEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        highlightEntity.Property(x => x.Word).HasColumnType("text").IsRequired(false);
        highlightEntity.HasIndex(x => new
        {
            x.GuildId, x.UserId
        });

        #endregion

        #region HighlightSettings

        var highlightSettingsEntity = modelBuilder.Entity<HighlightSettings>();
        highlightSettingsEntity.HasKey(x => x.Id);
        highlightSettingsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        highlightSettingsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        highlightSettingsEntity.Property(x => x.IgnoredChannels).HasColumnType("text").IsRequired(false);
        highlightSettingsEntity.Property(x => x.IgnoredUsers).HasColumnType("text").IsRequired(false);
        highlightSettingsEntity.Property(x => x.HighlightsOn).HasDefaultValue(false);

        #endregion

        #region IgnoredLogChannel

        var ignoredLogChannelEntity = modelBuilder.Entity<IgnoredLogChannel>();
        ignoredLogChannelEntity.HasKey(x => x.Id);
        ignoredLogChannelEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        ignoredLogChannelEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        ignoredLogChannelEntity.HasIndex(x => x.ChannelId);

        #endregion

        #region InviteCount

        var inviteCountEntity = modelBuilder.Entity<InviteCount>();
        inviteCountEntity.HasKey(x => x.Id);
        inviteCountEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        inviteCountEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        inviteCountEntity.Property(x => x.Count).HasColumnType("integer");

        #endregion

        #region InviteCountSettings

        var inviteCountSettingsEntity = modelBuilder.Entity<InviteCountSettings>();
        inviteCountSettingsEntity.HasKey(x => x.Id);
        inviteCountSettingsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        inviteCountSettingsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        inviteCountSettingsEntity.Property(x => x.MinAccountAge).HasColumnType("interval");

        #endregion

        #region InvitedBy

        var invitedByEntity = modelBuilder.Entity<InvitedBy>();
        invitedByEntity.HasKey(x => x.Id);
        invitedByEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        invitedByEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        invitedByEntity.HasIndex(x => new
        {
            x.UserId, x.InviterId, x.GuildId
        });

        #endregion

        #region JoinLeaveLogs

        var joinLeaveLogsEntity = modelBuilder.Entity<JoinLeaveLogs>();
        joinLeaveLogsEntity.HasKey(x => x.Id);
        joinLeaveLogsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        joinLeaveLogsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        joinLeaveLogsEntity.Property(x => x.IsJoin).HasDefaultValue(true);
        joinLeaveLogsEntity.HasIndex(x => new
        {
            x.GuildId, x.UserId
        });

        #endregion

        #region LockdownChannelPermissions

        var lockdownChannelPermissionsEntity = modelBuilder.Entity<LockdownChannelPermissions>();
        lockdownChannelPermissionsEntity.HasKey(x => x.Id);
        lockdownChannelPermissionsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        lockdownChannelPermissionsEntity.Property(x => x.AllowPermissions).HasColumnType("numeric(20, 0)");
        lockdownChannelPermissionsEntity.Property(x => x.DenyPermissions).HasColumnType("numeric(20, 0)");

        #endregion

        #region LoggingV2

        var loggingV2Entity = modelBuilder.Entity<LoggingV2>();
        loggingV2Entity.HasKey(x => x.Id);
        loggingV2Entity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        loggingV2Entity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        loggingV2Entity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)");
        loggingV2Entity.HasIndex(x => x.GuildId);

        #endregion

        #region MessageCount

        var messageCountEntity = modelBuilder.Entity<MessageCount>();
        messageCountEntity.HasKey(x => x.Id);
        messageCountEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        messageCountEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        messageCountEntity.Property(x => x.Count).HasColumnType("numeric(20, 0)");
        messageCountEntity.Property(x => x.RecentTimestamps).HasColumnType("text");

        #endregion

        #region MultiGreet

        var multiGreetEntity = modelBuilder.Entity<MultiGreet>();
        multiGreetEntity.HasKey(x => x.Id);
        multiGreetEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        multiGreetEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        multiGreetEntity.Property(x => x.Message).HasColumnType("text").IsRequired(false);
        multiGreetEntity.Property(x => x.GreetBots).HasDefaultValue(false);
        multiGreetEntity.Property(x => x.DeleteTime).HasDefaultValue(1);
        multiGreetEntity.Property(x => x.Disabled).HasDefaultValue(false);
        multiGreetEntity.Property(x => x.WebhookUrl).HasColumnType("text").IsRequired(false);

        #endregion

        #region MusicPlaylist

        var musicPlaylistEntity = modelBuilder.Entity<MusicPlaylist>();
        musicPlaylistEntity.HasKey(x => x.Id);
        musicPlaylistEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        musicPlaylistEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        musicPlaylistEntity.Property(x => x.Name).HasColumnType("text").IsRequired(false);
        musicPlaylistEntity.Property(x => x.Author).HasColumnType("text").IsRequired(false);
        musicPlaylistEntity.HasMany(x => x.Songs).WithOne().OnDelete(DeleteBehavior.Cascade);

        #endregion

        #region MusicPlayerSettings

        var musicPlayerSettingsEntity = modelBuilder.Entity<MusicPlayerSettings>();
        musicPlayerSettingsEntity.HasKey(x => x.Id);
        musicPlayerSettingsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        musicPlayerSettingsEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)");
        musicPlayerSettingsEntity.Property(x => x.Volume).HasDefaultValue(100);
        musicPlayerSettingsEntity.Property(x => x.AutoPlay).HasDefaultValue(0);
        musicPlayerSettingsEntity.Property(x => x.PlayerRepeat).HasConversion<int>();

        #endregion

        #region MutedUserId

        var mutedUserIdEntity = modelBuilder.Entity<MutedUserId>();
        mutedUserIdEntity.HasKey(x => x.Id);
        mutedUserIdEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        mutedUserIdEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        mutedUserIdEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)");
        mutedUserIdEntity.Property(x => x.roles).HasColumnType("text").IsRequired(false);
        mutedUserIdEntity.Property(x => x.GuildConfigId).HasColumnType("int").IsRequired();

        #endregion

        #region GlobalUserBalance

        var globalUserBalanceEntity = modelBuilder.Entity<GlobalUserBalance>();
        globalUserBalanceEntity.HasKey(x => x.Id);
        globalUserBalanceEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        globalUserBalanceEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        globalUserBalanceEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        globalUserBalanceEntity.Property(x => x.Balance).HasColumnType("bigint").IsRequired();

        #endregion

        #region GuildUserBalance

        var guildUserBalanceEntity = modelBuilder.Entity<GuildUserBalance>();
        guildUserBalanceEntity.HasKey(x => x.Id);
        guildUserBalanceEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        guildUserBalanceEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        guildUserBalanceEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        guildUserBalanceEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        guildUserBalanceEntity.Property(x => x.Balance).HasColumnType("bigint").IsRequired();

        #endregion

        #region TransactionHistory

        var transactionHistoryEntity = modelBuilder.Entity<TransactionHistory>();
        transactionHistoryEntity.HasKey(x => x.Id);
        transactionHistoryEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        transactionHistoryEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        transactionHistoryEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        transactionHistoryEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired(false);
        transactionHistoryEntity.Property(x => x.Amount).HasColumnType("bigint").IsRequired();
        transactionHistoryEntity.Property(x => x.Description).HasColumnType("text").IsRequired(false);

        #endregion

        #region NsfwBlacklistedTag

        var nsfwBlacklistedTagEntity = modelBuilder.Entity<NsfwBlacklitedTag>();
        nsfwBlacklistedTagEntity.HasKey(x => x.Id);
        nsfwBlacklistedTagEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        nsfwBlacklistedTagEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        nsfwBlacklistedTagEntity.Property(x => x.GuildConfigId).HasColumnType("int").IsRequired();
        nsfwBlacklistedTagEntity.Property(x => x.Tag).HasColumnType("text").IsRequired(false);

        #endregion

        #region OwnerOnly

        var ownerOnlyEntity = modelBuilder.Entity<OwnerOnly>();
        ownerOnlyEntity.HasKey(x => x.Id);
        ownerOnlyEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        ownerOnlyEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        ownerOnlyEntity.Property(x => x.Owners).HasColumnType("text").IsRequired(false);
        ownerOnlyEntity.Property(x => x.GptTokensUsed).HasColumnType("int").IsRequired();
        ownerOnlyEntity.Property(x => x.CurrencyEmote).HasColumnType("text").IsRequired(false);
        ownerOnlyEntity.Property(x => x.RewardAmount).HasColumnType("int").IsRequired();
        ownerOnlyEntity.Property(x => x.RewardTimeoutSeconds).HasColumnType("int").IsRequired();

        #endregion

        #region Permission

        var permissionEntity = modelBuilder.Entity<Permission>();
        permissionEntity.HasKey(x => x.Id);
        permissionEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        permissionEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        permissionEntity.HasOne(p => p.Next).WithOne(p => p.Previous).IsRequired(false);
        permissionEntity.Property(x => x.PrimaryTarget).HasColumnType("int").IsRequired();
        permissionEntity.Property(x => x.PrimaryTargetId).HasColumnType("numeric(20, 0)").IsRequired();
        permissionEntity.Property(x => x.SecondaryTarget).HasColumnType("int").IsRequired();
        permissionEntity.Property(x => x.SecondaryTargetName).HasColumnType("text").IsRequired(false);
        permissionEntity.Property(x => x.State).HasColumnType("boolean").IsRequired();

        #endregion

        #region PlaylistSong

        var playlistSongEntity = modelBuilder.Entity<PlaylistSong>();
        playlistSongEntity.HasKey(x => x.Id);
        playlistSongEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        playlistSongEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        playlistSongEntity.Property(x => x.MusicPlaylistId).HasColumnType("int").IsRequired();
        playlistSongEntity.Property(x => x.Provider).HasColumnType("text").IsRequired(false);
        playlistSongEntity.Property(x => x.ProviderType).HasColumnType("int").IsRequired();
        playlistSongEntity.Property(x => x.Title).HasColumnType("text").IsRequired(false);
        playlistSongEntity.Property(x => x.Uri).HasColumnType("text").IsRequired(false);
        playlistSongEntity.Property(x => x.Query).HasColumnType("text").IsRequired(false);

        #endregion

        #region Polls

        var pollEntity = modelBuilder.Entity<Polls>();
        pollEntity.HasKey(x => x.Id);
        pollEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        pollEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        pollEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        pollEntity.Property(x => x.ChannelId).HasColumnType("numeric(20, 0)").IsRequired();
        pollEntity.Property(x => x.Question).HasColumnType("text").IsRequired(false);
        pollEntity.HasMany(x => x.Answers).WithOne().IsRequired();
        pollEntity.Property(x => x.PollType).HasColumnType("int").IsRequired();
        pollEntity.HasMany(x => x.Votes).WithOne().IsRequired();

        #endregion

        #region PollAnswers

        var pollAnswerEntity = modelBuilder.Entity<PollAnswers>();
        pollAnswerEntity.HasKey(x => x.Id);
        pollAnswerEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        pollAnswerEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        pollAnswerEntity.Property(x => x.Text).HasColumnType("text").IsRequired(false);
        pollAnswerEntity.Property(x => x.Index).HasColumnType("int").IsRequired();

        #endregion

        #region PollVote

        var pollVoteEntity = modelBuilder.Entity<PollVote>();
        pollVoteEntity.HasKey(x => x.Id);
        pollVoteEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        pollVoteEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        pollVoteEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        pollVoteEntity.Property(x => x.VoteIndex).HasColumnType("int").IsRequired();
        pollVoteEntity.Property(x => x.PollId).HasColumnType("int").IsRequired();

        #endregion

        #region PublishUserBlacklist

        var publishUserBlacklistEntity = modelBuilder.Entity<PublishUserBlacklist>();
        publishUserBlacklistEntity.HasKey(x => x.Id);
        publishUserBlacklistEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        publishUserBlacklistEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        publishUserBlacklistEntity.Property(x => x.ChannelId).HasColumnType("numeric(20, 0)").IsRequired();
        publishUserBlacklistEntity.Property(x => x.User).HasColumnType("numeric(20, 0)").IsRequired();

        #endregion

        #region PublishWordBlacklist

        var publishWordBlacklistEntity = modelBuilder.Entity<PublishWordBlacklist>();
        publishWordBlacklistEntity.HasKey(x => x.Id);
        publishWordBlacklistEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        publishWordBlacklistEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        publishWordBlacklistEntity.Property(x => x.ChannelId).HasColumnType("numeric(20, 0)").IsRequired();
        publishWordBlacklistEntity.Property(x => x.Word).HasColumnType("text").IsRequired(false);

        #endregion

        #region Quote

        var quoteEntity = modelBuilder.Entity<Quote>();
        quoteEntity.HasKey(x => x.Id);
        quoteEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        quoteEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        quoteEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        quoteEntity.Property(x => x.Keyword).HasColumnType("text").IsRequired();
        quoteEntity.Property(x => x.AuthorName).HasColumnType("text").IsRequired();
        quoteEntity.Property(x => x.AuthorId).HasColumnType("numeric(20, 0)").IsRequired();
        quoteEntity.Property(x => x.Text).HasColumnType("text").IsRequired();
        quoteEntity.Property(x => x.UseCount).HasColumnType("bigint").IsRequired();

        #endregion

        #region ReactionRoleMessage

        var reactionRoleMessageEntity = modelBuilder.Entity<ReactionRoleMessage>();
        reactionRoleMessageEntity.HasKey(x => x.Id);
        reactionRoleMessageEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        reactionRoleMessageEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        reactionRoleMessageEntity.Property(x => x.GuildConfigId).HasColumnType("int").IsRequired();
        reactionRoleMessageEntity.Property(x => x.ChannelId).HasColumnType("numeric(20, 0)").IsRequired();
        reactionRoleMessageEntity.Property(x => x.MessageId).HasColumnType("numeric(20, 0)").IsRequired();
        reactionRoleMessageEntity.HasMany(x => x.ReactionRoles).WithOne().IsRequired();
        reactionRoleMessageEntity.Property(x => x.Exclusive).HasColumnType("boolean").IsRequired();
        reactionRoleMessageEntity.Property(x => x.Index).HasColumnType("int").IsRequired();

        #endregion

        #region ReactionRole

        var reactionRoleEntity = modelBuilder.Entity<ReactionRole>();
        reactionRoleEntity.HasKey(x => x.Id);
        reactionRoleEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        reactionRoleEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        reactionRoleEntity.Property(x => x.EmoteName).HasColumnType("text").IsRequired(false);
        reactionRoleEntity.Property(x => x.RoleId).HasColumnType("numeric(20, 0)").IsRequired();
        reactionRoleEntity.Property(x => x.ReactionRoleMessageId).HasColumnType("int").IsRequired();

        #endregion

        #region Reminder

        var reminderEntity = modelBuilder.Entity<Reminder>();
        reminderEntity.HasKey(x => x.Id);
        reminderEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        reminderEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        reminderEntity.Property(x => x.When).HasColumnType("timestamp").IsRequired();
        reminderEntity.Property(x => x.ChannelId).HasColumnType("numeric(20, 0)").IsRequired();
        reminderEntity.Property(x => x.ServerId).HasColumnType("numeric(20, 0)").IsRequired();
        reminderEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        reminderEntity.Property(x => x.Message).HasColumnType("text").IsRequired(false);
        reminderEntity.Property(x => x.IsPrivate).HasDefaultValue(false).IsRequired();

        #endregion

        #region Repeater

        var repeaterEntity = modelBuilder.Entity<Repeater>();
        repeaterEntity.HasKey(x => x.Id);
        repeaterEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        repeaterEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        repeaterEntity.Property(x => x.GuildConfigId).HasColumnType("int").IsRequired();
        repeaterEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        repeaterEntity.Property(x => x.ChannelId).HasColumnType("numeric(20, 0)").IsRequired();
        repeaterEntity.Property(x => x.LastMessageId).HasColumnType("numeric(20, 0)").IsRequired(false);
        repeaterEntity.Property(x => x.Message).HasColumnType("text").IsRequired(false);
        repeaterEntity.Property(x => x.Interval).HasColumnType("text").IsRequired(false);
        repeaterEntity.Property(x => x.StartTimeOfDay).HasColumnType("text").IsRequired(false);
        repeaterEntity.Property(x => x.NoRedundant).HasDefaultValue(false).IsRequired();

        #endregion

        #region RoleConnectionAuthStorage

        var roleConnectionAuthStorageEntity = modelBuilder.Entity<RoleConnectionAuthStorage>();
        roleConnectionAuthStorageEntity.HasKey(x => x.Id);
        roleConnectionAuthStorageEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        roleConnectionAuthStorageEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        roleConnectionAuthStorageEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        roleConnectionAuthStorageEntity.Property(x => x.Scopes).HasColumnType("text").IsRequired(false);
        roleConnectionAuthStorageEntity.Property(x => x.Token).HasColumnType("text").IsRequired(false);
        roleConnectionAuthStorageEntity.Property(x => x.RefreshToken).HasColumnType("text").IsRequired(false);
        roleConnectionAuthStorageEntity.Property(x => x.ExpiresAt).HasColumnType("timestamp").IsRequired();

        #endregion

        #region RoleGreet

        var roleGreetEntity = modelBuilder.Entity<RoleGreet>();
        roleGreetEntity.HasKey(x => x.Id);
        roleGreetEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        roleGreetEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        roleGreetEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        roleGreetEntity.Property(x => x.RoleId).HasColumnType("numeric(20, 0)").IsRequired();
        roleGreetEntity.Property(x => x.ChannelId).HasColumnType("numeric(20, 0)").IsRequired();
        roleGreetEntity.Property(x => x.GreetBots).HasDefaultValue(false).IsRequired();
        roleGreetEntity.Property(x => x.Message).HasColumnType("text").IsRequired(false);
        roleGreetEntity.Property(x => x.DeleteTime).HasColumnType("int").IsRequired();
        roleGreetEntity.Property(x => x.WebhookUrl).HasColumnType("text").IsRequired(false);
        roleGreetEntity.Property(x => x.Disabled).HasDefaultValue(false).IsRequired();

        #endregion

        #region RoleMonitoringSettings

        var roleMonitoringSettingsEntity = modelBuilder.Entity<RoleMonitoringSettings>();
        roleMonitoringSettingsEntity.HasKey(x => x.Id);
        roleMonitoringSettingsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        roleMonitoringSettingsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        roleMonitoringSettingsEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        roleMonitoringSettingsEntity.Property(x => x.DefaultPunishmentAction).HasColumnType("int").IsRequired();

        #endregion

        #region RoleStateSettings

        var roleStateSettingsEntity = modelBuilder.Entity<RoleStateSettings>();
        roleStateSettingsEntity.HasKey(x => x.Id);
        roleStateSettingsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        roleStateSettingsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        roleStateSettingsEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        roleStateSettingsEntity.Property(x => x.Enabled).HasDefaultValue(false).IsRequired();
        roleStateSettingsEntity.Property(x => x.ClearOnBan).HasDefaultValue(false).IsRequired();
        roleStateSettingsEntity.Property(x => x.IgnoreBots).HasDefaultValue(true).IsRequired();
        roleStateSettingsEntity.Property(x => x.DeniedRoles).HasColumnType("text").IsRequired(false);
        roleStateSettingsEntity.Property(x => x.DeniedUsers).HasColumnType("text").IsRequired(false);

        #endregion

        #region RotatingPlayingStatus

        var rotatingPlayingStatusEntity = modelBuilder.Entity<RotatingPlayingStatus>();
        rotatingPlayingStatusEntity.HasKey(x => x.Id);
        rotatingPlayingStatusEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        rotatingPlayingStatusEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        rotatingPlayingStatusEntity.Property(x => x.Status).HasColumnType("text").IsRequired(false);
        rotatingPlayingStatusEntity.Property(x => x.Type).HasColumnType("int").IsRequired();

        #endregion

        #region SelfAssignedRole

        var selfAssignedRoleEntity = modelBuilder.Entity<SelfAssignedRole>();
        selfAssignedRoleEntity.HasKey(x => x.Id);
        selfAssignedRoleEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        selfAssignedRoleEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        selfAssignedRoleEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        selfAssignedRoleEntity.Property(x => x.RoleId).HasColumnType("numeric(20, 0)").IsRequired();
        selfAssignedRoleEntity.Property(x => x.Group).HasColumnType("int").IsRequired();
        selfAssignedRoleEntity.Property(x => x.LevelRequirement).HasColumnType("int").IsRequired();

        #endregion

        #region ServerRecoveryStore

        var serverRecoveryStoreEntity = modelBuilder.Entity<ServerRecoveryStore>();
        serverRecoveryStoreEntity.HasKey(x => x.Id);
        serverRecoveryStoreEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        serverRecoveryStoreEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        serverRecoveryStoreEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        serverRecoveryStoreEntity.Property(x => x.RecoveryKey).HasColumnType("text").IsRequired(false);
        serverRecoveryStoreEntity.Property(x => x.TwoFactorKey).HasColumnType("text").IsRequired(false);

        #endregion

        #region StarboardPosts

        var starboardPostsEntity = modelBuilder.Entity<StarboardPosts>();
        starboardPostsEntity.HasKey(x => x.Id);
        starboardPostsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        starboardPostsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        starboardPostsEntity.Property(x => x.MessageId).HasColumnType("numeric(20, 0)").IsRequired();
        starboardPostsEntity.Property(x => x.PostId).HasColumnType("numeric(20, 0)").IsRequired();

        #endregion

        #region Starboards

        var starboardsEntity = modelBuilder.Entity<Starboards>();
        starboardsEntity.HasKey(x => x.Id);
        starboardsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        starboardsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        starboardsEntity.Property(x => x.Star).HasColumnType("text").HasDefaultValue("⭐").IsRequired(false);
        starboardsEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        starboardsEntity.Property(x => x.StarboardChannel).HasColumnType("numeric(20, 0)").IsRequired();
        starboardsEntity.Property(x => x.StarboardThreshold).HasColumnType("int").HasDefaultValue(3).IsRequired();
        starboardsEntity.Property(x => x.RepostThreshold).HasColumnType("int").HasDefaultValue(5).IsRequired();
        starboardsEntity.Property(x => x.StarboardAllowBots).HasDefaultValue(true).IsRequired();
        starboardsEntity.Property(x => x.StarboardRemoveOnDelete).HasDefaultValue(false).IsRequired();
        starboardsEntity.Property(x => x.StarboardRemoveOnReactionsClear).HasDefaultValue(false).IsRequired();
        starboardsEntity.Property(x => x.StarboardRemoveOnBelowThreshold).HasDefaultValue(true).IsRequired();
        starboardsEntity.Property(x => x.UseStarboardBlacklist).HasDefaultValue(true).IsRequired();
        starboardsEntity.Property(x => x.StarboardCheckChannels).HasColumnType("text").HasDefaultValue("0")
            .IsRequired(false);

        #endregion

        #region StatusRolesTable

        var statusRolesTableEntity = modelBuilder.Entity<StatusRolesTable>();
        statusRolesTableEntity.HasKey(x => x.Id);
        statusRolesTableEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        statusRolesTableEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        statusRolesTableEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        statusRolesTableEntity.Property(x => x.Status).HasColumnType("text").IsRequired(false);
        statusRolesTableEntity.Property(x => x.ToAdd).HasColumnType("text").IsRequired(false);
        statusRolesTableEntity.Property(x => x.ToRemove).HasColumnType("text").IsRequired(false);
        statusRolesTableEntity.Property(x => x.StatusEmbed).HasColumnType("text").IsRequired(false);
        statusRolesTableEntity.Property(x => x.ReaddRemoved).HasDefaultValue(false).IsRequired();
        statusRolesTableEntity.Property(x => x.RemoveAdded).HasDefaultValue(true).IsRequired();
        statusRolesTableEntity.Property(x => x.StatusChannelId).HasColumnType("numeric(20, 0)").IsRequired();

        #endregion

        #region StreamRoleSettings

        var streamRoleSettingsEntity = modelBuilder.Entity<StreamRoleSettings>();
        streamRoleSettingsEntity.HasKey(x => x.Id);
        streamRoleSettingsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        streamRoleSettingsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        streamRoleSettingsEntity.Property(x => x.GuildConfigId).HasColumnType("int").IsRequired();
        streamRoleSettingsEntity.Property(x => x.Enabled).HasDefaultValue(false).IsRequired();
        streamRoleSettingsEntity.Property(x => x.AddRoleId).HasColumnType("numeric(20, 0)").IsRequired();
        streamRoleSettingsEntity.Property(x => x.FromRoleId).HasColumnType("numeric(20, 0)").IsRequired();
        streamRoleSettingsEntity.Property(x => x.Keyword).HasColumnType("text").IsRequired(false);

        #endregion

        #region StreamRoleBlacklistedUser

        var streamRoleBlacklistedUserEntity = modelBuilder.Entity<StreamRoleBlacklistedUser>();
        streamRoleBlacklistedUserEntity.HasKey(x => x.Id);
        streamRoleBlacklistedUserEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        streamRoleBlacklistedUserEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        streamRoleBlacklistedUserEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        streamRoleBlacklistedUserEntity.Property(x => x.Username).HasColumnType("text").IsRequired(false);
        streamRoleBlacklistedUserEntity.Property(x => x.StreamRoleSettingsId).HasColumnType("int").IsRequired();

        #endregion

        #region StreamRoleWhitelistedUser

        var streamRoleWhitelistedUserEntity = modelBuilder.Entity<StreamRoleWhitelistedUser>();
        streamRoleWhitelistedUserEntity.HasKey(x => x.Id);
        streamRoleWhitelistedUserEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        streamRoleWhitelistedUserEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        streamRoleWhitelistedUserEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        streamRoleWhitelistedUserEntity.Property(x => x.Username).HasColumnType("text").IsRequired(false);
        streamRoleWhitelistedUserEntity.Property(x => x.StreamRoleSettingsId).HasColumnType("int").IsRequired();

        #endregion

        #region SuggestionsModel

        var suggestionsModelEntity = modelBuilder.Entity<SuggestionsModel>();
        suggestionsModelEntity.HasKey(x => x.Id);
        suggestionsModelEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        suggestionsModelEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        suggestionsModelEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        suggestionsModelEntity.Property(x => x.SuggestionId).HasColumnType("numeric(20, 0)").IsRequired();
        suggestionsModelEntity.Property(x => x.Suggestion).HasColumnType("text").IsRequired(false);
        suggestionsModelEntity.Property(x => x.MessageId).HasColumnType("numeric(20, 0)").IsRequired();
        suggestionsModelEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        suggestionsModelEntity.Property(x => x.EmoteCount1).HasColumnType("int").HasDefaultValue(0).IsRequired();
        suggestionsModelEntity.Property(x => x.EmoteCount2).HasColumnType("int").HasDefaultValue(0).IsRequired();
        suggestionsModelEntity.Property(x => x.EmoteCount3).HasColumnType("int").HasDefaultValue(0).IsRequired();
        suggestionsModelEntity.Property(x => x.EmoteCount4).HasColumnType("int").HasDefaultValue(0).IsRequired();
        suggestionsModelEntity.Property(x => x.EmoteCount5).HasColumnType("int").HasDefaultValue(0).IsRequired();
        suggestionsModelEntity.Property(x => x.StateChangeUser).HasColumnType("numeric(20, 0)").HasDefaultValue(0)
            .IsRequired();
        suggestionsModelEntity.Property(x => x.StateChangeCount).HasColumnType("numeric(20, 0)").HasDefaultValue(0)
            .IsRequired();
        suggestionsModelEntity.Property(x => x.StateChangeMessageId).HasColumnType("numeric(20, 0)").HasDefaultValue(0)
            .IsRequired();
        suggestionsModelEntity.Property(x => x.CurrentState).HasColumnType("int").HasDefaultValue(0).IsRequired();

        #endregion

        #region SuggestThreads

        var suggestThreadsEntity = modelBuilder.Entity<SuggestThreads>();
        suggestThreadsEntity.HasKey(x => x.Id);
        suggestThreadsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        suggestThreadsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        suggestThreadsEntity.Property(x => x.MessageId).HasColumnType("numeric(20, 0)").IsRequired();
        suggestThreadsEntity.Property(x => x.ThreadChannelId).HasColumnType("numeric(20, 0)").IsRequired();

        #endregion

        #region SuggestVotes

        var suggestVotesEntity = modelBuilder.Entity<SuggestVotes>();
        suggestVotesEntity.HasKey(x => x.Id);
        suggestVotesEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        suggestVotesEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        suggestVotesEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        suggestVotesEntity.Property(x => x.MessageId).HasColumnType("numeric(20, 0)").IsRequired();
        suggestVotesEntity.Property(x => x.EmotePicked).HasColumnType("int").IsRequired();

        #endregion

        #region TicketButton

        var ticketButtonEntity = modelBuilder.Entity<TicketButton>();
        ticketButtonEntity.HasKey(x => x.Id);
        ticketButtonEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        ticketButtonEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        ticketButtonEntity.Property(x => x.TicketPanelId).HasColumnType("int").IsRequired();
        ticketButtonEntity.Property(x => x.Label).HasColumnType("text").IsRequired();
        ticketButtonEntity.Property(x => x.Emoji).HasColumnType("text").IsRequired();
        ticketButtonEntity.Property(x => x.OpenMessage).HasColumnType("text").IsRequired();

        #endregion

        #region TicketPanel

        var ticketPanelEntity = modelBuilder.Entity<TicketPanel>();
        ticketPanelEntity.HasKey(x => x.Id);
        ticketPanelEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        ticketPanelEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        ticketPanelEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        ticketPanelEntity.Property(x => x.ChannelId).HasColumnType("numeric(20, 0)").IsRequired();
        ticketPanelEntity.Property(x => x.MessageJson).HasColumnType("text").IsRequired(false).HasDefaultValue("");
        ticketPanelEntity.HasMany(x => x.Buttons).WithOne().HasForeignKey(x => x.TicketPanelId);

        #endregion

        #region UnbanTimer

        var unbanTimerEntity = modelBuilder.Entity<UnbanTimer>();
        unbanTimerEntity.HasKey(x => x.Id);
        unbanTimerEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        unbanTimerEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        unbanTimerEntity.Property(x => x.GuildConfigId).HasColumnType("int").IsRequired();
        unbanTimerEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        unbanTimerEntity.Property(x => x.UnbanAt).HasColumnType("timestamp without time zone").IsRequired();

        #endregion

        #region UnmuteTimer

        var unmuteTimerEntity = modelBuilder.Entity<UnmuteTimer>();
        unmuteTimerEntity.HasKey(x => x.Id);
        unmuteTimerEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        unmuteTimerEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        unmuteTimerEntity.Property(x => x.GuildConfigId).HasColumnType("int").IsRequired(false);
        unmuteTimerEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        unmuteTimerEntity.Property(x => x.UnmuteAt).HasColumnType("timestamp without time zone").IsRequired();

        #endregion

        #region UnroleTimer

        var unroleTimerEntity = modelBuilder.Entity<UnroleTimer>();
        unroleTimerEntity.HasKey(x => x.Id);
        unroleTimerEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        unroleTimerEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        unroleTimerEntity.Property(x => x.GuildConfigId).HasColumnType("int").IsRequired();
        unroleTimerEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        unroleTimerEntity.Property(x => x.RoleId).HasColumnType("numeric(20, 0)").IsRequired();
        unroleTimerEntity.Property(x => x.UnbanAt).HasColumnType("timestamp without time zone").IsRequired();

        #endregion

        #region Usernames

        var usernamesEntity = modelBuilder.Entity<Usernames>();
        usernamesEntity.HasKey(x => x.Id);
        usernamesEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        usernamesEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        usernamesEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        usernamesEntity.Property(x => x.Username).HasColumnType("text").IsRequired(false);

        #endregion

        #region UserRoleStates

        var userRoleStatesEntity = modelBuilder.Entity<UserRoleStates>();
        userRoleStatesEntity.HasKey(x => x.Id);
        userRoleStatesEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        userRoleStatesEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        userRoleStatesEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        userRoleStatesEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        userRoleStatesEntity.Property(x => x.UserName).HasColumnType("text").IsRequired(false);
        userRoleStatesEntity.Property(x => x.SavedRoles).HasColumnType("text").IsRequired(false);

        #endregion

        #region UserXpStats

        var userXpStatsEntity = modelBuilder.Entity<UserXpStats>();
        userXpStatsEntity.HasKey(x => x.Id);
        userXpStatsEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        userXpStatsEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        userXpStatsEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        userXpStatsEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        userXpStatsEntity.Property(x => x.Xp).HasColumnType("int").IsRequired();
        userXpStatsEntity.Property(x => x.AwardedXp).HasColumnType("int").IsRequired();
        userXpStatsEntity.Property(x => x.NotifyOnLevelUp).HasColumnType("int").IsRequired();
        userXpStatsEntity.Property(x => x.LastLevelUp).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();

        #endregion

        #region VcRoleInfo

        var vcRoleInfoEntity = modelBuilder.Entity<VcRoleInfo>();
        vcRoleInfoEntity.HasKey(x => x.Id);
        vcRoleInfoEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        vcRoleInfoEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        vcRoleInfoEntity.Property(x => x.GuildConfigId).HasColumnType("int").IsRequired();
        vcRoleInfoEntity.Property(x => x.VoiceChannelId).HasColumnType("numeric(20, 0)").IsRequired();
        vcRoleInfoEntity.Property(x => x.RoleId).HasColumnType("numeric(20, 0)").IsRequired();

        #endregion

        #region VoteRoles

        var voteRolesEntity = modelBuilder.Entity<VoteRoles>();
        voteRolesEntity.HasKey(x => x.Id);
        voteRolesEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        voteRolesEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        voteRolesEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        voteRolesEntity.Property(x => x.RoleId).HasColumnType("numeric(20, 0)").IsRequired();
        voteRolesEntity.Property(x => x.Timer).HasColumnType("int").IsRequired();

        #endregion

        #region Votes

        var votesEntity = modelBuilder.Entity<Votes>();
        votesEntity.HasKey(x => x.Id);
        votesEntity.Property(x => x.Id).HasColumnType("int").ValueGeneratedOnAdd();
        votesEntity.Property(x => x.DateAdded).IsRequired(false).HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        votesEntity.Property(x => x.GuildId).HasColumnType("numeric(20, 0)").IsRequired();
        votesEntity.Property(x => x.UserId).HasColumnType("numeric(20, 0)").IsRequired();
        votesEntity.Property(x => x.BotId).HasColumnType("numeric(20, 0)").IsRequired();

        #endregion
    }
}