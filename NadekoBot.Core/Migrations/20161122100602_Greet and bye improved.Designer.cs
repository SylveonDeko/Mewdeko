using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NadekoBot.Core.Services.Database;

namespace NadekoBot.Migrations
{
    [DbContext(typeof(NadekoContext))]
    [Migration("20161122100602_Greet and bye improved")]
    partial class Greetandbyeimproved
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.0-rtm-22752");

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.BlacklistItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int?>("BotConfigId");

                    b.Property<ulong>("ItemId");

                    b.Property<int>("Type");

                    b.HasKey("Id");

                    b.HasIndex("BotConfigId");

                    b.ToTable("BlacklistItem");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.BotConfig", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("BufferSize");

                    b.Property<float>("CurrencyGenerationChance");

                    b.Property<int>("CurrencyGenerationCooldown");

                    b.Property<string>("CurrencyName");

                    b.Property<string>("CurrencyPluralName");

                    b.Property<string>("CurrencySign");

                    b.Property<string>("DMHelpString");

                    b.Property<bool>("ForwardMessages");

                    b.Property<bool>("ForwardToAllOwners");

                    b.Property<string>("HelpString");

                    b.Property<int>("MigrationVersion");

                    b.Property<string>("RemindMessageFormat");

                    b.Property<bool>("RotatingStatuses");

                    b.HasKey("Id");

                    b.ToTable("BotConfig");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.ClashCaller", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("BaseDestroyed");

                    b.Property<string>("CallUser");

                    b.Property<int>("ClashWarId");

                    b.Property<int?>("SequenceNumber");

                    b.Property<int>("Stars");

                    b.Property<DateTime>("TimeAdded");

                    b.HasKey("Id");

                    b.HasIndex("ClashWarId");

                    b.ToTable("ClashCallers");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.ClashWar", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("ChannelId");

                    b.Property<string>("EnemyClan");

                    b.Property<ulong>("GuildId");

                    b.Property<int>("Size");

                    b.Property<DateTime>("StartedAt");

                    b.Property<int>("WarState");

                    b.HasKey("Id");

                    b.ToTable("ClashOfClans");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.CommandCooldown", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("CommandName");

                    b.Property<int?>("GuildConfigId");

                    b.Property<int>("Seconds");

                    b.HasKey("Id");

                    b.HasIndex("GuildConfigId");

                    b.ToTable("CommandCooldown");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.ConvertUnit", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("InternalTrigger");

                    b.Property<decimal>("Modifier");

                    b.Property<string>("UnitType");

                    b.HasKey("Id");

                    b.ToTable("ConversionUnits");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.Currency", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("Amount");

                    b.Property<ulong>("UserId");

                    b.HasKey("Id");

                    b.HasIndex("UserId")
                        .IsUnique();

                    b.ToTable("Currency");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.CurrencyTransaction", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("Amount");

                    b.Property<string>("Reason");

                    b.Property<ulong>("UserId");

                    b.HasKey("Id");

                    b.ToTable("CurrencyTransactions");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.CustomReaction", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong?>("GuildId");

                    b.Property<bool>("IsRegex");

                    b.Property<bool>("OwnerOnly");

                    b.Property<string>("Response");

                    b.Property<string>("Trigger");

                    b.HasKey("Id");

                    b.ToTable("CustomReactions");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.Donator", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("Amount");

                    b.Property<string>("Name");

                    b.Property<ulong>("UserId");

                    b.HasKey("Id");

                    b.HasIndex("UserId")
                        .IsUnique();

                    b.ToTable("Donators");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.EightBallResponse", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int?>("BotConfigId");

                    b.Property<string>("Text");

                    b.HasKey("Id");

                    b.HasIndex("BotConfigId");

                    b.ToTable("EightBallResponses");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.FilterChannelId", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("ChannelId");

                    b.Property<int?>("GuildConfigId");

                    b.Property<int?>("GuildConfigId1");

                    b.HasKey("Id");

                    b.HasIndex("GuildConfigId");

                    b.HasIndex("GuildConfigId1");

                    b.ToTable("FilterChannelId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.FilteredWord", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int?>("GuildConfigId");

                    b.Property<string>("Word");

                    b.HasKey("Id");

                    b.HasIndex("GuildConfigId");

                    b.ToTable("FilteredWord");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.FollowedStream", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("ChannelId");

                    b.Property<int?>("GuildConfigId");

                    b.Property<ulong>("GuildId");

                    b.Property<int>("Type");

                    b.Property<string>("Username");

                    b.HasKey("Id");

                    b.HasIndex("GuildConfigId");

                    b.ToTable("FollowedStream");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.GCChannelId", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("ChannelId");

                    b.Property<int?>("GuildConfigId");

                    b.HasKey("Id");

                    b.HasIndex("GuildConfigId");

                    b.ToTable("GCChannelId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.GuildConfig", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("AutoAssignRoleId");

                    b.Property<bool>("AutoDeleteByeMessages");

                    b.Property<int>("AutoDeleteByeMessagesTimer");

                    b.Property<bool>("AutoDeleteGreetMessages");

                    b.Property<int>("AutoDeleteGreetMessagesTimer");

                    b.Property<bool>("AutoDeleteSelfAssignedRoleMessages");

                    b.Property<ulong>("ByeMessageChannelId");

                    b.Property<string>("ChannelByeMessageText");

                    b.Property<string>("ChannelGreetMessageText");

                    b.Property<bool>("CleverbotEnabled");

                    b.Property<float>("DefaultMusicVolume");

                    b.Property<bool>("DeleteMessageOnCommand");

                    b.Property<string>("DmGreetMessageText");

                    b.Property<bool>("ExclusiveSelfAssignedRoles");

                    b.Property<bool>("FilterInvites");

                    b.Property<bool>("FilterWords");

                    b.Property<ulong>("GreetMessageChannelId");

                    b.Property<ulong>("GuildId");

                    b.Property<int?>("LogSettingId");

                    b.Property<string>("MuteRoleName");

                    b.Property<string>("PermissionRole");

                    b.Property<int?>("RootPermissionId");

                    b.Property<bool>("SendChannelByeMessage");

                    b.Property<bool>("SendChannelGreetMessage");

                    b.Property<bool>("SendDmGreetMessage");

                    b.Property<bool>("VerbosePermissions");

                    b.Property<bool>("VoicePlusTextEnabled");

                    b.HasKey("Id");

                    b.HasIndex("GuildId")
                        .IsUnique();

                    b.HasIndex("LogSettingId");

                    b.HasIndex("RootPermissionId");

                    b.ToTable("GuildConfigs");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.IgnoredLogChannel", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("ChannelId");

                    b.Property<int?>("LogSettingId");

                    b.HasKey("Id");

                    b.HasIndex("LogSettingId");

                    b.ToTable("IgnoredLogChannels");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.IgnoredVoicePresenceChannel", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("ChannelId");

                    b.Property<int?>("LogSettingId");

                    b.HasKey("Id");

                    b.HasIndex("LogSettingId");

                    b.ToTable("IgnoredVoicePresenceCHannels");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.LogSetting", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("ChannelCreated");

                    b.Property<bool>("ChannelDestroyed");

                    b.Property<ulong>("ChannelId");

                    b.Property<bool>("ChannelUpdated");

                    b.Property<bool>("IsLogging");

                    b.Property<bool>("LogUserPresence");

                    b.Property<bool>("LogVoicePresence");

                    b.Property<bool>("MessageDeleted");

                    b.Property<bool>("MessageUpdated");

                    b.Property<bool>("UserBanned");

                    b.Property<bool>("UserJoined");

                    b.Property<bool>("UserLeft");

                    b.Property<ulong>("UserPresenceChannelId");

                    b.Property<bool>("UserUnbanned");

                    b.Property<bool>("UserUpdated");

                    b.Property<ulong>("VoicePresenceChannelId");

                    b.HasKey("Id");

                    b.ToTable("LogSettings");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.ModulePrefix", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int?>("BotConfigId");

                    b.Property<string>("ModuleName");

                    b.Property<string>("Prefix");

                    b.HasKey("Id");

                    b.HasIndex("BotConfigId");

                    b.ToTable("ModulePrefixes");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.MusicPlaylist", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Author");

                    b.Property<ulong>("AuthorId");

                    b.Property<string>("Name");

                    b.HasKey("Id");

                    b.ToTable("MusicPlaylists");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.Permission", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int?>("NextId");

                    b.Property<int>("PrimaryTarget");

                    b.Property<ulong>("PrimaryTargetId");

                    b.Property<int>("SecondaryTarget");

                    b.Property<string>("SecondaryTargetName");

                    b.Property<bool>("State");

                    b.HasKey("Id");

                    b.HasIndex("NextId")
                        .IsUnique();

                    b.ToTable("Permission");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.PlayingStatus", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int?>("BotConfigId");

                    b.Property<string>("Status");

                    b.HasKey("Id");

                    b.HasIndex("BotConfigId");

                    b.ToTable("PlayingStatus");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.PlaylistSong", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int?>("MusicPlaylistId");

                    b.Property<string>("Provider");

                    b.Property<int>("ProviderType");

                    b.Property<string>("Query");

                    b.Property<string>("Title");

                    b.Property<string>("Uri");

                    b.HasKey("Id");

                    b.HasIndex("MusicPlaylistId");

                    b.ToTable("PlaylistSong");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.Quote", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("AuthorId");

                    b.Property<string>("AuthorName")
                        .IsRequired();

                    b.Property<ulong>("GuildId");

                    b.Property<string>("Keyword")
                        .IsRequired();

                    b.Property<string>("Text")
                        .IsRequired();

                    b.HasKey("Id");

                    b.ToTable("Quotes");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.RaceAnimal", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int?>("BotConfigId");

                    b.Property<string>("Icon");

                    b.Property<string>("Name");

                    b.HasKey("Id");

                    b.HasIndex("BotConfigId");

                    b.ToTable("RaceAnimals");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.Reminder", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("ChannelId");

                    b.Property<bool>("IsPrivate");

                    b.Property<string>("Message");

                    b.Property<ulong>("ServerId");

                    b.Property<ulong>("UserId");

                    b.Property<DateTime>("When");

                    b.HasKey("Id");

                    b.ToTable("Reminders");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.Repeater", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("ChannelId");

                    b.Property<ulong>("GuildId");

                    b.Property<TimeSpan>("Interval");

                    b.Property<string>("Message");

                    b.HasKey("Id");

                    b.HasIndex("ChannelId")
                        .IsUnique();

                    b.ToTable("Repeaters");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.SelfAssignedRole", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("GuildId");

                    b.Property<ulong>("RoleId");

                    b.HasKey("Id");

                    b.HasIndex("GuildId", "RoleId")
                        .IsUnique();

                    b.ToTable("SelfAssignableRoles");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.BlacklistItem", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.BotConfig")
                        .WithMany("Blacklist")
                        .HasForeignKey("BotConfigId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.ClashCaller", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.ClashWar", "ClashWar")
                        .WithMany("Bases")
                        .HasForeignKey("ClashWarId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.CommandCooldown", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.GuildConfig")
                        .WithMany("CommandCooldowns")
                        .HasForeignKey("GuildConfigId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.EightBallResponse", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.BotConfig")
                        .WithMany("EightBallResponses")
                        .HasForeignKey("BotConfigId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.FilterChannelId", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.GuildConfig")
                        .WithMany("FilterInvitesChannelIds")
                        .HasForeignKey("GuildConfigId");

                    b.HasOne("NadekoBot.Core.Services.Database.Models.GuildConfig")
                        .WithMany("FilterWordsChannelIds")
                        .HasForeignKey("GuildConfigId1");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.FilteredWord", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.GuildConfig")
                        .WithMany("FilteredWords")
                        .HasForeignKey("GuildConfigId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.FollowedStream", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.GuildConfig")
                        .WithMany("FollowedStreams")
                        .HasForeignKey("GuildConfigId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.GCChannelId", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.GuildConfig")
                        .WithMany("GenerateCurrencyChannelIds")
                        .HasForeignKey("GuildConfigId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.GuildConfig", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.LogSetting", "LogSetting")
                        .WithMany()
                        .HasForeignKey("LogSettingId");

                    b.HasOne("NadekoBot.Core.Services.Database.Models.Permission", "RootPermission")
                        .WithMany()
                        .HasForeignKey("RootPermissionId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.IgnoredLogChannel", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.LogSetting", "LogSetting")
                        .WithMany("IgnoredChannels")
                        .HasForeignKey("LogSettingId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.IgnoredVoicePresenceChannel", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.LogSetting", "LogSetting")
                        .WithMany("IgnoredVoicePresenceChannelIds")
                        .HasForeignKey("LogSettingId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.ModulePrefix", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.BotConfig")
                        .WithMany("ModulePrefixes")
                        .HasForeignKey("BotConfigId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.Permission", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.Permission", "Next")
                        .WithOne("Previous")
                        .HasForeignKey("NadekoBot.Core.Services.Database.Models.Permission", "NextId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.PlayingStatus", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.BotConfig")
                        .WithMany("RotatingStatusMessages")
                        .HasForeignKey("BotConfigId");
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.PlaylistSong", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.MusicPlaylist")
                        .WithMany("Songs")
                        .HasForeignKey("MusicPlaylistId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("NadekoBot.Core.Services.Database.Models.RaceAnimal", b =>
                {
                    b.HasOne("NadekoBot.Core.Services.Database.Models.BotConfig")
                        .WithMany("RaceAnimals")
                        .HasForeignKey("BotConfigId");
                });
        }
    }
}
