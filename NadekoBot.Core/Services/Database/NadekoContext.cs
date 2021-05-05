using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Core.Services.Impl;
using NadekoBot.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace NadekoBot.Core.Services.Database
{
    public class NadekoContextFactory : IDesignTimeDbContextFactory<NadekoContext>
    {
        public NadekoContext CreateDbContext(string[] args)
        {
            LogSetup.SetupLogger(-2);
            var optionsBuilder = new DbContextOptionsBuilder<NadekoContext>();
            IBotCredentials creds = new BotCredentials();
            var builder = new SqliteConnectionStringBuilder(creds.Db.ConnectionString);
            builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);
            optionsBuilder.UseSqlite(builder.ToString());
            var ctx = new NadekoContext(optionsBuilder.Options);
            ctx.Database.SetCommandTimeout(60);
            return ctx;
        }
    }

    public class NadekoContext : DbContext
    {
        public DbSet<BotConfig> BotConfig { get; set; }
        public DbSet<GuildConfig> GuildConfigs { get; set; }

        public DbSet<Quote> Quotes { get; set; }
        public DbSet<Reminder> Reminders { get; set; }
        public DbSet<SelfAssignedRole> SelfAssignableRoles { get; set; }
        public DbSet<MusicPlaylist> MusicPlaylists { get; set; }
        public DbSet<CustomReaction> CustomReactions { get; set; }
        public DbSet<CurrencyTransaction> CurrencyTransactions { get; set; }
        public DbSet<WaifuUpdate> WaifuUpdates { get; set; }
        public DbSet<Warning> Warnings { get; set; }
        public DbSet<UserXpStats> UserXpStats { get; set; }
        public DbSet<ClubInfo> Clubs { get; set; }

        //logging
        public DbSet<LogSetting> LogSettings { get; set; }
        public DbSet<IgnoredLogChannel> IgnoredLogChannels { get; set; }
        public DbSet<IgnoredVoicePresenceChannel> IgnoredVoicePresenceCHannels { get; set; }

        //orphans xD
        public DbSet<EightBallResponse> EightBallResponses { get; set; }
        public DbSet<RaceAnimal> RaceAnimals { get; set; }
        public DbSet<RewardedUser> RewardedUsers { get; set; }
        public DbSet<Stake> Stakes { get; set; }
        public DbSet<PlantedCurrency> PlantedCurrency { get; set; }
        public DbSet<BanTemplate> BanTemplates { get; set; }
        public DbSet<DiscordPermOverride> DiscordPermOverrides { get; set; }

        public NadekoContext(DbContextOptions<NadekoContext> options) : base(options)
        {
        }

        public void EnsureSeedData()
        {
            if (!BotConfig.Any())
            {
                var bc = new BotConfig();

                bc.RaceAnimals.AddRange(new HashSet<RaceAnimal>
                {
                    new RaceAnimal { Icon = "🐼", Name = "Panda" },
                    new RaceAnimal { Icon = "🐻", Name = "Bear" },
                    new RaceAnimal { Icon = "🐧", Name = "Pengu" },
                    new RaceAnimal { Icon = "🐨", Name = "Koala" },
                    new RaceAnimal { Icon = "🐬", Name = "Dolphin" },
                    new RaceAnimal { Icon = "🐞", Name = "Ladybird" },
                    new RaceAnimal { Icon = "🦀", Name = "Crab" },
                    new RaceAnimal { Icon = "🦄", Name = "Unicorn" }
                });
                bc.EightBallResponses.AddRange(new HashSet<EightBallResponse>
                {
                    new EightBallResponse() { Text = "Most definitely yes" },
                    new EightBallResponse() { Text = "For sure" },
                    new EightBallResponse() { Text = "Totally!" },
                    new EightBallResponse() { Text = "Of course!" },
                    new EightBallResponse() { Text = "As I see it, yes" },
                    new EightBallResponse() { Text = "My sources say yes" },
                    new EightBallResponse() { Text = "Yes" },
                    new EightBallResponse() { Text = "Most likely" },
                    new EightBallResponse() { Text = "Perhaps" },
                    new EightBallResponse() { Text = "Maybe" },
                    new EightBallResponse() { Text = "Not sure" },
                    new EightBallResponse() { Text = "It is uncertain" },
                    new EightBallResponse() { Text = "Ask me again later" },
                    new EightBallResponse() { Text = "Don't count on it" },
                    new EightBallResponse() { Text = "Probably not" },
                    new EightBallResponse() { Text = "Very doubtful" },
                    new EightBallResponse() { Text = "Most likely no" },
                    new EightBallResponse() { Text = "Nope" },
                    new EightBallResponse() { Text = "No" },
                    new EightBallResponse() { Text = "My sources say no" },
                    new EightBallResponse() { Text = "Dont even think about it" },
                    new EightBallResponse() { Text = "Definitely no" },
                    new EightBallResponse() { Text = "NO - It may cause disease contraction" }
                });

                BotConfig.Add(bc);

                this.SaveChanges();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region QUOTES

            var quoteEntity = modelBuilder.Entity<Quote>();
            quoteEntity.HasIndex(x => x.GuildId);
            quoteEntity.HasIndex(x => x.Keyword);

            #endregion

            #region GuildConfig

            var configEntity = modelBuilder.Entity<GuildConfig>();
            configEntity
                .HasIndex(c => c.GuildId)
                .IsUnique();

            modelBuilder.Entity<AntiSpamSetting>()
                .HasOne(x => x.GuildConfig)
                .WithOne(x => x.AntiSpamSetting);

            modelBuilder.Entity<AntiRaidSetting>()
                .HasOne(x => x.GuildConfig)
                .WithOne(x => x.AntiRaidSetting);

            modelBuilder.Entity<FeedSub>()
                .HasAlternateKey(x => new { x.GuildConfigId, x.Url });

            modelBuilder.Entity<PlantedCurrency>()
                .HasIndex(x => x.MessageId)
                .IsUnique();

            modelBuilder.Entity<PlantedCurrency>()
                .HasIndex(x => x.ChannelId);

            configEntity.HasIndex(x => x.WarnExpireHours)
                .IsUnique(false);

            #endregion

            #region streamrole
            modelBuilder.Entity<StreamRoleSettings>()
                .HasOne(x => x.GuildConfig)
                .WithOne(x => x.StreamRole);
            #endregion

            #region BotConfig
            var botConfigEntity = modelBuilder.Entity<BotConfig>();

            botConfigEntity.Property(x => x.XpMinutesTimeout)
                .HasDefaultValue(5);

            botConfigEntity.Property(x => x.XpPerMessage)
                .HasDefaultValue(3);

            botConfigEntity.Property(x => x.VoiceXpPerMinute)
                .HasDefaultValue(0);

            botConfigEntity.Property(x => x.MaxXpMinutes)
                .HasDefaultValue(720);

            botConfigEntity.Property(x => x.PatreonCurrencyPerCent)
                .HasDefaultValue(1.0f);

            botConfigEntity.Property(x => x.WaifuGiftMultiplier)
                .HasDefaultValue(1);

            botConfigEntity.Property(x => x.OkColor)
                .HasDefaultValue("00e584");

            botConfigEntity.Property(x => x.ErrorColor)
                .HasDefaultValue("ee281f");

            botConfigEntity.Property(x => x.LastUpdate)
                .HasDefaultValue(new DateTime(2018, 5, 5, 0, 0, 0, 0, DateTimeKind.Utc));

            #endregion

            #region Self Assignable Roles

            var selfassignableRolesEntity = modelBuilder.Entity<SelfAssignedRole>();

            selfassignableRolesEntity
                .HasIndex(s => new { s.GuildId, s.RoleId })
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

            #region Waifus

            var wi = modelBuilder.Entity<WaifuInfo>();
            wi.HasOne(x => x.Waifu)
                .WithOne();

            wi.HasIndex(x => x.Price);
            wi.HasIndex(x => x.ClaimerId);

            var wu = modelBuilder.Entity<WaifuUpdate>();
            #endregion

            #region DiscordUser

            var du = modelBuilder.Entity<DiscordUser>();
            du.HasAlternateKey(w => w.UserId);
            du.HasOne(x => x.Club)
               .WithMany(x => x.Users)
               .IsRequired(false);

            du.Property(x => x.LastLevelUp)
                .HasDefaultValue(new DateTime(2017, 9, 21, 20, 53, 13, 305, DateTimeKind.Local));

            du.HasIndex(x => x.TotalXp);
            du.HasIndex(x => x.CurrencyAmount);
            du.HasIndex(x => x.UserId);


            #endregion

            #region Warnings
            var warn = modelBuilder.Entity<Warning>();
            warn.HasIndex(x => x.GuildId);
            warn.HasIndex(x => x.UserId);
            warn.HasIndex(x => x.DateAdded);
            #endregion

            #region PatreonRewards
            var pr = modelBuilder.Entity<RewardedUser>();
            pr.HasIndex(x => x.PatreonUserId)
                .IsUnique();
            #endregion

            #region XpStats
            var xps = modelBuilder.Entity<UserXpStats>();
            xps
                .HasIndex(x => new { x.UserId, x.GuildId })
                .IsUnique();

            xps
                .Property(x => x.LastLevelUp)
                .HasDefaultValue(new DateTime(2017, 9, 21, 20, 53, 13, 307, DateTimeKind.Local));

            xps.HasIndex(x => x.UserId);
            xps.HasIndex(x => x.GuildId);
            xps.HasIndex(x => x.Xp);
            xps.HasIndex(x => x.AwardedXp);

            #endregion

            #region XpSettings
            modelBuilder.Entity<XpSettings>()
                .HasOne(x => x.GuildConfig)
                .WithOne(x => x.XpSettings);
            #endregion

            #region XpRoleReward
            modelBuilder.Entity<XpRoleReward>()
                .HasIndex(x => new { x.XpSettingsId, x.Level })
                .IsUnique();
            #endregion

            #region Club
            var ci = modelBuilder.Entity<ClubInfo>();
            ci.HasOne(x => x.Owner)
              .WithOne()
              .HasForeignKey<ClubInfo>(x => x.OwnerId);


            ci.HasAlternateKey(x => new { x.Name, x.Discrim });
            #endregion

            #region ClubManytoMany

            modelBuilder.Entity<ClubApplicants>()
                .HasKey(t => new { t.ClubId, t.UserId });

            modelBuilder.Entity<ClubApplicants>()
                .HasOne(pt => pt.User)
                .WithMany();

            modelBuilder.Entity<ClubApplicants>()
                .HasOne(pt => pt.Club)
                .WithMany(x => x.Applicants);

            modelBuilder.Entity<ClubBans>()
                .HasKey(t => new { t.ClubId, t.UserId });

            modelBuilder.Entity<ClubBans>()
                .HasOne(pt => pt.User)
                .WithMany();

            modelBuilder.Entity<ClubBans>()
                .HasOne(pt => pt.Club)
                .WithMany(x => x.Bans);

            #endregion

            #region Polls
            modelBuilder.Entity<Poll>()
                .HasIndex(x => x.GuildId)
                .IsUnique();
            #endregion

            #region CurrencyTransactions
            modelBuilder.Entity<CurrencyTransaction>()
                .HasIndex(x => x.UserId)
                .IsUnique(false);
            #endregion

            #region Reminders
            modelBuilder.Entity<Reminder>()
                .HasIndex(x => x.When);
            #endregion

            #region  GroupName
            modelBuilder.Entity<GroupName>()
                .HasIndex(x => new { x.GuildConfigId, x.Number })
                .IsUnique();

            modelBuilder.Entity<GroupName>()
                .HasOne(x => x.GuildConfig)
                .WithMany(x => x.SelfAssignableRoleGroupNames)
                .IsRequired();
            #endregion
            
            #region BanTemplate

            modelBuilder.Entity<BanTemplate>()
                .HasIndex(x => x.GuildId)
                .IsUnique();

            #endregion
            
            #region Perm Override

            modelBuilder.Entity<DiscordPermOverride>()
                .HasIndex(x => new {x.GuildId, x.Command})
                .IsUnique();

            #endregion
            
            #region BotConfigMigrations

            var bcEntity = modelBuilder.Entity<BotConfig>();
            bcEntity.Property(bc => bc.HasMigratedBotSettings)
                .HasDefaultValue(1);

            #endregion
        }
    }
}
