using Mewdeko.Common.Attributes.DB;
using Mewdeko.Services.Impl;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database
{
    /// <summary>
    /// Represents the database context for Mewdeko.
    /// </summary>
    public class MewdekoContext : DbContext
    {
        private BotCredentials creds;
        /// <summary>
        /// Initializes a new instance of the <see cref="MewdekoContext"/> class.
        /// </summary>
        /// <param name="options">The options to be used by the DbContext.</param>
        public MewdekoContext(DbContextOptions options) : base(options)
        {
            creds = new BotCredentials();
        }

        /// <summary>
        /// Gets or sets the global user balances.
        /// </summary>
        public DbSet<GlobalUserBalance> GlobalUserBalances { get; set; }

        /// <summary>
        /// Gets or sets the giveaway users.
        /// </summary>
        public DbSet<GiveawayUsers> GiveawayUsers { get; set; }

        /// <summary>
        /// The bots reviews, can be added via dashboard or the bot itself
        /// </summary>
        public DbSet<BotReviews> BotReviews { get; set; }

        /// <summary>
        /// Message Counts
        /// </summary>
        public DbSet<MessageCount> MessageCounts { get; set; }

        /// <summary>
        /// Gets or sets the anti-alt settings.
        /// </summary>
        public DbSet<AntiAltSetting> AntiAltSettings { get; set; }

        /// <summary>
        /// Gets or sets the anti-spam settings.
        /// </summary>
        public DbSet<AntiSpamSetting> AntiSpamSettings { get; set; }

        /// <summary>
        /// Gets or sets the anti-raid settings.
        /// </summary>
        public DbSet<AntiRaidSetting> AntiRaidSettings { get; set; }

        /// <summary>
        /// Gets or sets ticket panels
        /// </summary>
        public DbSet<TicketPanel> TicketPanels { get; set; }

        /// <summary>
        /// Gets or sets ticket buttons
        /// </summary>
        public DbSet<TicketButton> TicketButtons { get; set; }

        /// <summary>
        /// Gets or sets the anti-spam ignore settings.
        /// </summary>
        public DbSet<AntiSpamIgnore> AntiSpamIgnore { get; set; }

        /// <summary>
        /// Gets or sets the guild user balances.
        /// </summary>
        public DbSet<GuildUserBalance> GuildUserBalances { get; set; }

        /// <summary>
        /// Gets or sets the transaction histories.
        /// </summary>
        public DbSet<TransactionHistory> TransactionHistories { get; set; }

        /// <summary>
        /// Gets or sets the auto publish settings.
        /// </summary>
        public DbSet<AutoPublish> AutoPublish { get; set; }

        /// <summary>
        /// Gets or sets the auto ban roles.
        /// </summary>
        public DbSet<AutoBanRoles> AutoBanRoles { get; set; }

        /// <summary>
        /// Gets or sets the publish word blacklists.
        /// </summary>
        public DbSet<PublishWordBlacklist> PublishWordBlacklists { get; set; }

        /// <summary>
        /// Gets or sets the publish user blacklists.
        /// </summary>
        public DbSet<PublishUserBlacklist> PublishUserBlacklists { get; set; }

        /// <summary>
        /// Gets or sets the join and leave logs.
        /// </summary>
        public DbSet<JoinLeaveLogs> JoinLeaveLogs { get; set; }

        /// <summary>
        /// Gets or sets the guild configurations.
        /// </summary>
        public DbSet<GuildConfig> GuildConfigs { get; set; }

        /// <summary>
        /// Gets or sets the suggestions.
        /// </summary>
        public DbSet<SuggestionsModel> Suggestions { get; set; }

        /// <summary>
        /// Gets or sets the filtered words.
        /// </summary>
        public DbSet<FilteredWord> FilteredWords { get; set; }

        /// <summary>
        /// Gets or sets the owner only settings.
        /// </summary>
        public DbSet<OwnerOnly> OwnerOnly { get; set; }

        /// <summary>
        /// Gets or sets the warnings (second version).
        /// </summary>
        public DbSet<Warning2> Warnings2 { get; set; }

        /// <summary>
        /// Gets or sets the templates.
        /// </summary>
        public DbSet<Template> Templates { get; set; }

        /// <summary>
        /// Gets or sets the server recovery store.
        /// </summary>
        public DbSet<ServerRecoveryStore> ServerRecoveryStore { get; set; }

        /// <summary>
        /// Gets or sets the AFK settings.
        /// </summary>
        public DbSet<Afk> Afk { get; set; }

        /// <summary>
        /// Gets or sets the multi greet settings.
        /// </summary>
        public DbSet<MultiGreet> MultiGreets { get; set; }

        /// <summary>
        /// Gets or sets the user role states.
        /// </summary>
        public DbSet<UserRoleStates> UserRoleStates { get; set; }

        /// <summary>
        /// Gets or sets the role state settings.
        /// </summary>
        public DbSet<RoleStateSettings> RoleStateSettings { get; set; }

        /// <summary>
        /// Gets or sets the giveaways.
        /// </summary>
        public DbSet<Giveaways> Giveaways { get; set; }

        /// <summary>
        /// Gets or sets the starboard posts.
        /// </summary>
        public DbSet<StarboardPosts> Starboard { get; set; }

        /// <summary>
        /// Gets or sets the quotes.
        /// </summary>
        public DbSet<Quote> Quotes { get; set; }

        /// <summary>
        /// Gets or sets the reminders.
        /// </summary>
        public DbSet<Reminder> Reminders { get; set; }

        /// <summary>
        /// Gets or sets the confessions.
        /// </summary>
        public DbSet<Confessions> Confessions { get; set; }

        /// <summary>
        /// Gets or sets the self-assigned roles.
        /// </summary>
        public DbSet<SelfAssignedRole> SelfAssignableRoles { get; set; }

        /// <summary>
        /// Gets or sets the role greets.
        /// </summary>
        public DbSet<RoleGreet> RoleGreets { get; set; }

        /// <summary>
        /// Gets or sets the highlights.
        /// </summary>
        public DbSet<Highlights> Highlights { get; set; }

        /// <summary>
        /// Gets or sets the command statistics.
        /// </summary>
        public DbSet<CommandStats> CommandStats { get; set; }

        /// <summary>
        /// Gets or sets the highlight settings.
        /// </summary>
        public DbSet<HighlightSettings> HighlightSettings { get; set; }

        /// <summary>
        /// Gets or sets the music playlists.
        /// </summary>
        public DbSet<MusicPlaylist> MusicPlaylists { get; set; }

        /// <summary>
        /// Gets or sets the chat triggers.
        /// </summary>
        public DbSet<ChatTriggers> ChatTriggers { get; set; }

        /// <summary>
        /// Gets or sets the music player settings.
        /// </summary>
        public DbSet<MusicPlayerSettings> MusicPlayerSettings { get; set; }

        /// <summary>
        /// Gets or sets the warnings.
        /// </summary>
        public DbSet<Warning> Warnings { get; set; }

        /// <summary>
        /// Gets or sets the user XP stats.
        /// </summary>
        public DbSet<UserXpStats> UserXpStats { get; set; }

        /// <summary>
        /// Gets or sets the vote roles.
        /// </summary>
        public DbSet<VoteRoles> VoteRoles { get; set; }

        /// <summary>
        /// Gets or sets the polls.
        /// </summary>
        public DbSet<Polls> Poll { get; set; }

        /// <summary>
        /// Gets or sets the command cooldowns.
        /// </summary>
        public DbSet<CommandCooldown> CommandCooldown { get; set; }

        /// <summary>
        /// Gets or sets the suggest votes.
        /// </summary>
        public DbSet<SuggestVotes> SuggestVotes { get; set; }

        /// <summary>
        /// Gets or sets the suggest threads.
        /// </summary>
        public DbSet<SuggestThreads> SuggestThreads { get; set; }

        /// <summary>
        /// Gets or sets the votes.
        /// </summary>
        public DbSet<Models.Votes> Votes { get; set; }

        /// <summary>
        /// Gets or sets the command aliases.
        /// </summary>
        public DbSet<CommandAlias> CommandAliases { get; set; }

        /// <summary>
        /// Gets or sets the log settings.
        /// </summary>
        public DbSet<LogSetting> LogSettings { get; set; }

        /// <summary>
        /// Gets or sets the ignored log channels.
        /// </summary>
        public DbSet<IgnoredLogChannel> IgnoredLogChannels { get; set; }

        /// <summary>
        /// Gets or sets the rotating playing statuses.
        /// </summary>
        public DbSet<RotatingPlayingStatus> RotatingStatus { get; set; }

        /// <summary>
        /// Gets or sets the blacklist entries.
        /// </summary>
        public DbSet<BlacklistEntry> Blacklist { get; set; }

        /// <summary>
        /// Gets or sets the auto commands.
        /// </summary>
        public DbSet<AutoCommand> AutoCommands { get; set; }

        /// <summary>
        /// Gets or sets the auto ban words.
        /// </summary>
        public DbSet<AutoBanEntry> AutoBanWords { get; set; }

        /// <summary>
        /// Gets or sets the status roles.
        /// </summary>
        public DbSet<StatusRolesTable> StatusRoles { get; set; }

        /// <summary>
        /// Gets or sets the ban templates.
        /// </summary>
        public DbSet<BanTemplate> BanTemplates { get; set; }

        /// <summary>
        /// Gets or sets the discord permission overrides.
        /// </summary>
        public DbSet<DiscordPermOverride> DiscordPermOverrides { get; set; }

        /// <summary>
        /// Gets or sets the discord users.
        /// </summary>
        public DbSet<DiscordUser> DiscordUser { get; set; }

        /// <summary>
        /// Gets or sets the feed subscriptions.
        /// </summary>
        public DbSet<FeedSub> FeedSubs { get; set; }

        /// <summary>
        /// Gets or sets the muted user IDs.
        /// </summary>
        public DbSet<MutedUserId> MutedUserIds { get; set; }

        /// <summary>
        /// Gets or sets the playlist songs.
        /// </summary>
        public DbSet<PlaylistSong> PlaylistSongs { get; set; }

        /// <summary>
        /// Gets or sets the poll votes.
        /// </summary>
        public DbSet<PollVote> PollVotes { get; set; }

        /// <summary>
        /// Gets or sets the role connection authentication storage.
        /// </summary>
        public DbSet<RoleConnectionAuthStorage> AuthCodes { get; set; }

        /// <summary>
        /// Gets or sets the repeaters.
        /// </summary>
        public DbSet<Repeater> Repeaters { get; set; }

        /// <summary>
        /// Gets or sets the unban timers.
        /// </summary>
        public DbSet<UnbanTimer> UnbanTimers { get; set; }

        /// <summary>
        /// Gets or sets the unmute timers.
        /// </summary>
        public DbSet<UnmuteTimer> UnmuteTimers { get; set; }

        /// <summary>
        /// Gets or sets the unrole timers.
        /// </summary>
        public DbSet<UnroleTimer> UnroleTimers { get; set; }

        /// <summary>
        /// Gets or sets the voice channel role information.
        /// </summary>
        public DbSet<VcRoleInfo> VcRoleInfos { get; set; }

        /// <summary>
        /// Gets or sets the warning punishments.
        /// </summary>
        public DbSet<WarningPunishment> WarningPunishments { get; set; }

        /// <summary>
        /// Gets or sets the second version of warning punishments.
        /// </summary>
        public DbSet<WarningPunishment2> WarningPunishments2 { get; set; }

        /// <summary>
        /// Configures the model that was discovered by convention from the entity types
        /// exposed in <see cref="DbSet{TEntity}"/> properties on your derived context.
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
            afkEntity.Property(x => x.WasTimed)
                .HasDefaultValue(false);

            #endregion

            #region AntiProtection

            modelBuilder.Entity<AntiSpamIgnore>()
                .Property(x => x.AntiSpamSettingId)
                .IsRequired(false);

            #endregion

            #region ChatTriggers

            var chatTriggerEntity = modelBuilder.Entity<ChatTriggers>();
            chatTriggerEntity.Property(x => x.IsRegex)
                .HasDefaultValue(false);

            chatTriggerEntity.Property(x => x.OwnerOnly)
                .HasDefaultValue(false);

            chatTriggerEntity.Property(x => x.AutoDeleteTrigger)
                .HasDefaultValue(false);

            chatTriggerEntity.Property(x => x.ReactToTrigger)
                .HasDefaultValue(false);

            chatTriggerEntity.Property(x => x.NoRespond)
                .HasDefaultValue(false);

            chatTriggerEntity.Property(x => x.DmResponse)
                .HasDefaultValue(false);

            chatTriggerEntity.Property(x => x.ContainsAnywhere)
                .HasDefaultValue(false);

            chatTriggerEntity.Property(x => x.AllowTarget)
                .HasDefaultValue(false);

            #endregion

            #region CommandStats

            var commandStatsEntity = modelBuilder.Entity<CommandStats>();

            commandStatsEntity.Property(x => x.IsSlash)
                .HasDefaultValue(false);

            commandStatsEntity.Property(x => x.Trigger)
                .HasDefaultValue(false);

            commandStatsEntity.Property(x => x.Module)
                .IsRequired(false);

            commandStatsEntity.Property(x => x.Module)
                .HasDefaultValue("");

            #endregion

            #region CommandAliases

            modelBuilder.Entity<CommandAlias>()
                .Property(x => x.GuildConfigId)
                .IsRequired(false);

            #endregion

            #region DelMsgOnCmdChannel

            var delMsgOnCmdChannelEntity = modelBuilder.Entity<DelMsgOnCmdChannel>();

            delMsgOnCmdChannelEntity.Property(x => x.State)
                .HasDefaultValue(true);

            #endregion

            #region QUOTES

            var quoteEntity = modelBuilder.Entity<Quote>();
            quoteEntity.HasIndex(x => x.GuildId);
            quoteEntity.HasIndex(x => x.Keyword);

            #endregion

            #region GuildConfig

            modelBuilder.Entity<UnmuteTimer>()
                .Property(x => x.GuildConfigId)
                .IsRequired(false);

            var configEntity = modelBuilder.Entity<GuildConfig>();
            configEntity
                .HasIndex(c => c.GuildId)
                .IsUnique();

            configEntity.HasOne(x => x.AntiSpamSetting)
                .WithOne()
                .HasForeignKey<AntiSpamSetting>(x => x.GuildConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            configEntity.HasOne(x => x.AntiRaidSetting)
                .WithOne()
                .HasForeignKey<AntiRaidSetting>(x => x.GuildConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            configEntity.Property(x => x.LogSettingId)
                .IsRequired(false);

            modelBuilder.Entity<GuildConfig>()
                .HasOne(x => x.AntiAltSetting)
                .WithOne()
                .HasForeignKey<AntiAltSetting>(x => x.GuildConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FeedSub>()
                .HasAlternateKey(x => new
                {
                    x.GuildConfigId, x.Url
                });

            configEntity.HasIndex(x => x.WarnExpireHours)
                .IsUnique(false);

            configEntity.Property(x => x.DeleteMessageOnCommand)
                .HasDefaultValue(false);

            configEntity.Property(x => x.StatsOptOut)
                .HasDefaultValue(false);

            configEntity.Property(x => x.DmOnGiveawayWin)
                .HasDefaultValue(true);

            configEntity.Property(x => x.SendDmGreetMessage)
                .HasDefaultValue(false);

            configEntity.Property(x => x.SendChannelGreetMessage)
                .HasDefaultValue(false);

            configEntity.Property(x => x.SendChannelByeMessage)
                .HasDefaultValue(false);

            configEntity.Property(x => x.StarboardAllowBots)
                .HasDefaultValue(false);

            configEntity.Property(x => x.StarboardRemoveOnDelete)
                .HasDefaultValue(false);

            configEntity.Property(x => x.StarboardRemoveOnReactionsClear)
                .HasDefaultValue(false);

            configEntity.Property(x => x.UseStarboardBlacklist)
                .HasDefaultValue(true);

            configEntity.Property(x => x.StarboardRemoveOnBelowThreshold)
                .HasDefaultValue(true);

            configEntity.Property(x => x.ArchiveOnDeny)
                .HasDefaultValue(false);

            configEntity.Property(x => x.ArchiveOnAccept)
                .HasDefaultValue(false);

            configEntity.Property(x => x.ArchiveOnImplement)
                .HasDefaultValue(false);

            configEntity.Property(x => x.ArchiveOnConsider)
                .HasDefaultValue(false);

            configEntity.Property(x => x.GBAction)
                .HasDefaultValue(false);

            #endregion

            #region HighlightSettings

            var highlightSettingsEntity = modelBuilder.Entity<HighlightSettings>();

            highlightSettingsEntity.Property(x => x.HighlightsOn)
                .HasDefaultValue(false);

            #endregion

            #region MultiGreets

            var multiGreetsEntity = modelBuilder.Entity<MultiGreet>();

            multiGreetsEntity.Property(x => x.GreetBots)
                .HasDefaultValue(false);

            multiGreetsEntity.Property(x => x.DeleteTime)
                .HasDefaultValue(1);

            multiGreetsEntity.Property(x => x.Disabled)
                .HasDefaultValue(false);

            multiGreetsEntity.Property(x => x.WebhookUrl)
                .HasDefaultValue(null);

            #endregion

            #region streamrole

            configEntity.HasOne(x => x.StreamRole)
                .WithOne()
                .HasForeignKey<StreamRoleSettings>(x => x.GuildConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            #endregion

            #region Self Assignable Roles

            var selfassignableRolesEntity = modelBuilder.Entity<SelfAssignedRole>();

            selfassignableRolesEntity
                .HasIndex(s => new
                {
                    s.GuildId, s.RoleId
                })
                .IsUnique();

            selfassignableRolesEntity
                .Property(x => x.Group)
                .HasDefaultValue(0);

            #endregion

            #region Permission

            var permissionEntity = modelBuilder.Entity<Permission>();
            permissionEntity
                .HasOne(p => p.Next)
                .WithOne(p => p.Previous)
                .IsRequired(false);

            #endregion

            #region MusicPlaylists

            var musicPlaylistEntity = modelBuilder.Entity<MusicPlaylist

>();

            musicPlaylistEntity
                .HasMany(p => p.Songs)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);

            #endregion

            #region DiscordUser

            var du = modelBuilder.Entity<DiscordUser>();
            du.HasAlternateKey(w => w.UserId);

            du.Property(x => x.LastLevelUp)
                .HasDefaultValue(new DateTime(2017, 9, 21, 20, 53, 13, 305, DateTimeKind.Local));

            du.HasIndex(x => x.TotalXp);
            du.HasIndex(x => x.UserId);
            du.Property(x => x.IsClubAdmin)
                .HasDefaultValue(false);

            du.Property(x => x.PronounsDisabled)
                .HasDefaultValue(false);

            du.Property(x => x.StatsOptOut)
                .HasDefaultValue(false);

            du.Property(x => x.IsDragon)
                .HasDefaultValue(false);

            du.Property(x => x.NotifyOnLevelUp)
                .HasDefaultValue(XpNotificationLocation.None);

            du.Property(x => x.ProfilePrivacy)
                .HasDefaultValue(Models.DiscordUser.ProfilePrivacyEnum.Public);

            du.Property(x => x.BirthdayDisplayMode)
                .HasDefaultValue(Models.DiscordUser.BirthdayDisplayModeEnum.Default);

            #endregion

            #region Warnings

            var warn = modelBuilder.Entity<Warning>();
            warn.HasIndex(x => x.GuildId);
            warn.HasIndex(x => x.UserId);
            warn.HasIndex(x => x.DateAdded);

            #endregion

            #region XpStats

            var xps = modelBuilder.Entity<UserXpStats>();
            xps
                .HasIndex(x => new
                {
                    x.UserId, x.GuildId
                })
                .IsUnique();

            xps
                .Property(x => x.LastLevelUp)
                .HasDefaultValue(new DateTime(2017, 9, 21, 20, 53, 13, 307, DateTimeKind.Local));

            xps.HasIndex(x => x.UserId);
            xps.HasIndex(x => x.GuildId);
            xps.HasIndex(x => x.Xp);
            xps.HasIndex(x => x.AwardedXp);

            #endregion

            #region Music

            modelBuilder.Entity<MusicPlayerSettings>()
                .HasIndex(x => x.GuildId)
                .IsUnique();

            modelBuilder.Entity<MusicPlayerSettings>()
                .Property(x => x.Volume)
                .HasDefaultValue(100);

            #endregion

            #region XpSettings

            configEntity.HasOne(x => x.XpSettings)
                .WithOne()
                .HasForeignKey<XpSettings>(x => x.GuildConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            #endregion

            #region XpRoleReward

            modelBuilder.Entity<XpRoleReward>()
                .HasIndex(x => new
                {
                    x.XpSettingsId, x.Level
                })
                .IsUnique();

            #endregion

            #region Polls

            modelBuilder.Entity<Polls>()
                .HasIndex(x => x.GuildId)
                .IsUnique();

            #endregion

            #region Reminders

            modelBuilder.Entity<Reminder>()
                .HasIndex(x => x.When);

            #endregion

            #region GroupName

            modelBuilder.Entity<GroupName>()
                .HasIndex(x => new
                {
                    x.GuildConfigId, x.Number
                })
                .IsUnique();

            configEntity.HasMany(x => x.SelfAssignableRoleGroupNames)
                .WithOne()
                .HasForeignKey(x => x.GuildConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            #endregion

            #region BanTemplate

            modelBuilder.Entity<BanTemplate>()
                .HasIndex(x => x.GuildId)
                .IsUnique();

            #endregion

            #region Perm Override

            modelBuilder.Entity<DiscordPermOverride>()
                .HasIndex(x => new
                {
                    x.GuildId, x.Command
                })
                .IsUnique();

            #endregion

            #region Tickets

            modelBuilder.Entity<TicketPanel>()
                .HasMany(p => p.Buttons)
                .WithOne()
                .HasForeignKey(b => b.TicketPanelId);

            #endregion
        }
    }
}