using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database;

public class MewdekoContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<GlobalUserBalance> GlobalUserBalances { get; set; }

    public DbSet<AntiAltSetting> AntiAltSettings { get; set; }

    public DbSet<AntiSpamSetting> AntiSpamSettings { get; set; }

    public DbSet<AntiRaidSetting> AntiRaidSettings { get; set; }

    public DbSet<AntiSpamIgnore> AntiSpamIgnore { get; set; }
    public DbSet<GuildUserBalance> GuildUserBalances { get; set; }
    public DbSet<TransactionHistory> TransactionHistories { get; set; }
    public DbSet<AutoPublish> AutoPublish { get; set; }
    public DbSet<AutoBanRoles> AutoBanRoles { get; set; }
    public DbSet<PublishWordBlacklist> PublishWordBlacklists { get; set; }
    public DbSet<PublishUserBlacklist> PublishUserBlacklists { get; set; }
    public DbSet<JoinLeaveLogs> JoinLeaveLogs { get; set; }
    public DbSet<GuildConfig> GuildConfigs { get; set; }

    public DbSet<SuggestionsModel> Suggestions { get; set; }
    public DbSet<FilteredWord> FilteredWords { get; set; }
    public DbSet<OwnerOnly> OwnerOnly { get; set; }

    // public DbSet<GlobalBanConfig> GlobalBanConfigs { get; set; }
    public DbSet<Warning2> Warnings2 { get; set; }
    public DbSet<Template> Templates { get; set; }
    public DbSet<ServerRecoveryStore> ServerRecoveryStore { get; set; }
    public DbSet<Afk> Afk { get; set; }
    public DbSet<MultiGreet> MultiGreets { get; set; }
    public DbSet<UserRoleStates> UserRoleStates { get; set; }
    public DbSet<RoleStateSettings> RoleStateSettings { get; set; }
    public DbSet<Giveaways> Giveaways { get; set; }
    public DbSet<StarboardPosts> Starboard { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<Reminder> Reminders { get; set; }
    public DbSet<Confessions> Confessions { get; set; }
    public DbSet<SelfAssignedRole> SelfAssignableRoles { get; set; }
    public DbSet<RoleGreet> RoleGreets { get; set; }
    public DbSet<Highlights> Highlights { get; set; }
    public DbSet<CommandStats> CommandStats { get; set; }
    public DbSet<HighlightSettings> HighlightSettings { get; set; }
    public DbSet<MusicPlaylist> MusicPlaylists { get; set; }
    public DbSet<ChatTriggers> ChatTriggers { get; set; }
    public DbSet<MusicPlayerSettings> MusicPlayerSettings { get; set; }
    public DbSet<Warning> Warnings { get; set; }
    public DbSet<UserXpStats> UserXpStats { get; set; }
    public DbSet<VoteRoles> VoteRoles { get; set; }
    public DbSet<Poll> Poll { get; set; }
    public DbSet<CommandCooldown> CommandCooldown { get; set; }
    public DbSet<SuggestVotes> SuggestVotes { get; set; }
    public DbSet<SuggestThreads> SuggestThreads { get; set; }
    public DbSet<Votes> Votes { get; set; }

    public DbSet<CommandAlias> CommandAliases { get; set; }

    //logging
    public DbSet<LogSetting> LogSettings { get; set; }
    public DbSet<IgnoredLogChannel> IgnoredLogChannels { get; set; }

    public DbSet<RotatingPlayingStatus> RotatingStatus { get; set; }
    public DbSet<BlacklistEntry> Blacklist { get; set; }
    public DbSet<AutoCommand> AutoCommands { get; set; }
    public DbSet<AutoBanEntry> AutoBanWords { get; set; }
    public DbSet<StatusRolesTable> StatusRoles { get; set; }
    public DbSet<BanTemplate> BanTemplates { get; set; }
    public DbSet<DiscordPermOverride> DiscordPermOverrides { get; set; }
    public DbSet<DiscordUser> DiscordUser { get; set; }

    public DbSet<FeedSub> FeedSubs { get; set; }

    public DbSet<MutedUserId> MutedUserIds { get; set; }

    public DbSet<PlaylistSong> PlaylistSongs { get; set; }

    public DbSet<PollVote> PollVotes { get; set; }

    public DbSet<RoleConnectionAuthStorage> AuthCodes { get; set; }

    public DbSet<Repeater> Repeaters { get; set; }

    public DbSet<RotatingPlayingStatus> RotatingPlayingStatuses { get; set; }

    public DbSet<UnbanTimer> UnbanTimers { get; set; }

    public DbSet<UnmuteTimer> UnmuteTimers { get; set; }

    public DbSet<UnroleTimer> UnroleTimers { get; set; }

    public DbSet<VcRoleInfo> VcRoleInfos { get; set; }

    public DbSet<WarningPunishment> WarningPunishments { get; set; }

    public DbSet<WarningPunishment2> WarningPunishments2 { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

        var musicPlaylistEntity = modelBuilder.Entity<MusicPlaylist>();

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

        modelBuilder.Entity<Poll>()
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
    }
}