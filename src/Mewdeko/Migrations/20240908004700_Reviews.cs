using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Migrations
{
    /// <inheritdoc />
    public partial class Reviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AFK",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    WasTimed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    When = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AFK", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Token = table.Column<string>(type: "text", nullable: true),
                    RefreshToken = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoBanRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoBanRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoBanWords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Word = table.Column<string>(type: "text", nullable: true),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoBanWords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommandText = table.Column<string>(type: "text", nullable: true),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelName = table.Column<string>(type: "text", nullable: true),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    GuildName = table.Column<string>(type: "text", nullable: true),
                    VoiceChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    VoiceChannelName = table.Column<string>(type: "text", nullable: true),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoPublish",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    BlacklistedUsers = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoPublish", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BanTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BanTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Blacklist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blacklist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatTriggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UseCount = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsRegex = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OwnerOnly = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Response = table.Column<string>(type: "text", nullable: true),
                    Trigger = table.Column<string>(type: "text", nullable: true),
                    PrefixType = table.Column<int>(type: "integer", nullable: false),
                    CustomPrefix = table.Column<string>(type: "text", nullable: true),
                    AutoDeleteTrigger = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReactToTrigger = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    NoRespond = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DmResponse = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ContainsAnywhere = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AllowTarget = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Reactions = table.Column<string>(type: "text", nullable: true),
                    GrantedRoles = table.Column<string>(type: "text", nullable: true),
                    RemovedRoles = table.Column<string>(type: "text", nullable: true),
                    RoleGrantType = table.Column<int>(type: "integer", nullable: false),
                    ValidTriggerTypes = table.Column<int>(type: "integer", nullable: false),
                    ApplicationCommandId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ApplicationCommandName = table.Column<string>(type: "text", nullable: true),
                    ApplicationCommandDescription = table.Column<string>(type: "text", nullable: true),
                    ApplicationCommandType = table.Column<int>(type: "integer", nullable: false),
                    EphemeralResponse = table.Column<bool>(type: "boolean", nullable: false),
                    CrosspostingChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CrosspostingWebhookUrl = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTriggers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameOrId = table.Column<string>(type: "text", nullable: true),
                    Module = table.Column<string>(type: "text", nullable: true, defaultValue: ""),
                    IsSlash = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Trigger = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Confessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ConfessNumber = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Confession = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Confessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscordPermOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Perm = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Command = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordPermOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscordUser",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    Discriminator = table.Column<string>(type: "text", nullable: true),
                    AvatarId = table.Column<string>(type: "text", nullable: true),
                    IsClubAdmin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TotalXp = table.Column<int>(type: "integer", nullable: false),
                    LastLevelUp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValue: new DateTime(2017, 9, 21, 20, 53, 13, 305, DateTimeKind.Local)),
                    NotifyOnLevelUp = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsDragon = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Pronouns = table.Column<string>(type: "text", nullable: true),
                    PronounsClearedReason = table.Column<string>(type: "text", nullable: true),
                    PronounsDisabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    ProfileImageUrl = table.Column<string>(type: "text", nullable: true),
                    ZodiacSign = table.Column<string>(type: "text", nullable: true),
                    ProfilePrivacy = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ProfileColor = table.Column<long>(type: "bigint", nullable: true),
                    Birthday = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SwitchFriendCode = table.Column<string>(type: "text", nullable: true),
                    BirthdayDisplayMode = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StatsOptOut = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordUser", x => x.Id);
                    table.UniqueConstraint("AK_DiscordUser_UserId", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Giveaways",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    When = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ServerId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Ended = table.Column<int>(type: "integer", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Winners = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Item = table.Column<string>(type: "text", nullable: true),
                    RestrictTo = table.Column<string>(type: "text", nullable: true),
                    BlacklistUsers = table.Column<string>(type: "text", nullable: true),
                    BlacklistRoles = table.Column<string>(type: "text", nullable: true),
                    Emote = table.Column<string>(type: "text", nullable: true),
                    UseButton = table.Column<bool>(type: "boolean", nullable: false),
                    UseCaptcha = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Giveaways", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiveawayUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GiveawayId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiveawayUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlobalUserBalance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Balance = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalUserBalance", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuildUserBalance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Balance = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildUserBalance", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Highlights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Word = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Highlights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HighlightSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IgnoredChannels = table.Column<string>(type: "text", nullable: true),
                    IgnoredUsers = table.Column<string>(type: "text", nullable: true),
                    HighlightsOn = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HighlightSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JoinLeaveLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsJoin = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinLeaveLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogOtherId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    MessageUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    MessageDeletedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ThreadCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ThreadDeletedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ThreadUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UsernameUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    NicknameUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AvatarUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserLeftId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserBannedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserUnbannedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserJoinedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserRoleAddedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserRoleRemovedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserMutedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LogUserPresenceId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LogVoicePresenceId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LogVoicePresenceTTSId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ServerUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RoleUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RoleDeletedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    EventCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RoleCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelDestroyedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    IsLogging = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageUpdated = table.Column<long>(type: "bigint", nullable: false),
                    MessageDeleted = table.Column<long>(type: "bigint", nullable: false),
                    UserJoined = table.Column<long>(type: "bigint", nullable: false),
                    UserLeft = table.Column<long>(type: "bigint", nullable: false),
                    UserBanned = table.Column<long>(type: "bigint", nullable: false),
                    UserUnbanned = table.Column<long>(type: "bigint", nullable: false),
                    UserUpdated = table.Column<long>(type: "bigint", nullable: false),
                    ChannelCreated = table.Column<long>(type: "bigint", nullable: false),
                    ChannelDestroyed = table.Column<long>(type: "bigint", nullable: false),
                    ChannelUpdated = table.Column<long>(type: "bigint", nullable: false),
                    LogUserPresence = table.Column<long>(type: "bigint", nullable: false),
                    UserPresenceChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LogVoicePresence = table.Column<long>(type: "bigint", nullable: false),
                    VoicePresenceChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Count = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RecentTimestamps = table.Column<string>(type: "text", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageCounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MultiGreets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    GreetBots = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleteTime = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    WebhookUrl = table.Column<string>(type: "text", nullable: true),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MultiGreets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MusicPlayerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PlayerRepeat = table.Column<int>(type: "integer", nullable: false),
                    MusicChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Volume = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    AutoDisconnect = table.Column<int>(type: "integer", nullable: false),
                    AutoPlay = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicPlayerSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MusicPlaylists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Author = table.Column<string>(type: "text", nullable: true),
                    AuthorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicPlaylists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OwnerOnly",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Owners = table.Column<string>(type: "text", nullable: true),
                    GptTokensUsed = table.Column<int>(type: "integer", nullable: false),
                    CurrencyEmote = table.Column<string>(type: "text", nullable: true),
                    RewardAmount = table.Column<int>(type: "integer", nullable: false),
                    RewardTimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerOnly", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NextId = table.Column<int>(type: "integer", nullable: false),
                    PrimaryTarget = table.Column<int>(type: "integer", nullable: false),
                    PrimaryTargetId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SecondaryTarget = table.Column<int>(type: "integer", nullable: false),
                    SecondaryTargetName = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permission_Permission_NextId",
                        column: x => x.NextId,
                        principalTable: "Permission",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Poll",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: true),
                    PollType = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Poll", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublishUserBlacklist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    User = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishUserBlacklist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublishWordBlacklist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Word = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishWordBlacklist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Keyword = table.Column<string>(type: "text", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: false),
                    AuthorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    UseCount = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    When = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ServerId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    IsPrivate = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleGreets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GreetBots = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    DeleteTime = table.Column<int>(type: "integer", nullable: false),
                    WebhookUrl = table.Column<string>(type: "text", nullable: true),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleGreets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleStateSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ClearOnBan = table.Column<bool>(type: "boolean", nullable: false),
                    IgnoreBots = table.Column<bool>(type: "boolean", nullable: false),
                    DeniedRoles = table.Column<string>(type: "text", nullable: true),
                    DeniedUsers = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleStateSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RotatingStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RotatingStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SelfAssignableRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Group = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LevelRequirement = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfAssignableRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerRecoveryStore",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RecoveryKey = table.Column<string>(type: "text", nullable: true),
                    TwoFactorKey = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerRecoveryStore", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Starboard",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PostId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Starboard", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: true),
                    ToAdd = table.Column<string>(type: "text", nullable: true),
                    ToRemove = table.Column<string>(type: "text", nullable: true),
                    StatusEmbed = table.Column<string>(type: "text", nullable: true),
                    ReaddRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveAdded = table.Column<bool>(type: "boolean", nullable: false),
                    StatusChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SuggestionId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Suggestion = table.Column<string>(type: "text", nullable: true),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EmoteCount1 = table.Column<int>(type: "integer", nullable: false),
                    EmoteCount2 = table.Column<int>(type: "integer", nullable: false),
                    EmoteCount3 = table.Column<int>(type: "integer", nullable: false),
                    EmoteCount4 = table.Column<int>(type: "integer", nullable: false),
                    EmoteCount5 = table.Column<int>(type: "integer", nullable: false),
                    StateChangeUser = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StateChangeCount = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StateChangeMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CurrentState = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SuggestThreads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ThreadChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuggestThreads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SuggestVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EmotePicked = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuggestVotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateBar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BarColor = table.Column<string>(type: "text", nullable: true),
                    BarPointAx = table.Column<int>(type: "integer", nullable: false),
                    BarPointAy = table.Column<int>(type: "integer", nullable: false),
                    BarPointBx = table.Column<int>(type: "integer", nullable: false),
                    BarPointBy = table.Column<int>(type: "integer", nullable: false),
                    BarLength = table.Column<int>(type: "integer", nullable: false),
                    BarTransparency = table.Column<byte>(type: "smallint", nullable: false),
                    BarDirection = table.Column<int>(type: "integer", nullable: false),
                    ShowBar = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateBar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateClub",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubIconX = table.Column<int>(type: "integer", nullable: false),
                    ClubIconY = table.Column<int>(type: "integer", nullable: false),
                    ClubIconSizeX = table.Column<int>(type: "integer", nullable: false),
                    ClubIconSizeY = table.Column<int>(type: "integer", nullable: false),
                    ShowClubIcon = table.Column<bool>(type: "boolean", nullable: false),
                    ClubNameColor = table.Column<string>(type: "text", nullable: true),
                    ClubNameFontSize = table.Column<int>(type: "integer", nullable: false),
                    ClubNameX = table.Column<int>(type: "integer", nullable: false),
                    ClubNameY = table.Column<int>(type: "integer", nullable: false),
                    ShowClubName = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateClub", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateGuild",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildLevelColor = table.Column<string>(type: "text", nullable: true),
                    GuildLevelFontSize = table.Column<int>(type: "integer", nullable: false),
                    GuildLevelX = table.Column<int>(type: "integer", nullable: false),
                    GuildLevelY = table.Column<int>(type: "integer", nullable: false),
                    ShowGuildLevel = table.Column<bool>(type: "boolean", nullable: false),
                    GuildRankColor = table.Column<string>(type: "text", nullable: true),
                    GuildRankFontSize = table.Column<int>(type: "integer", nullable: false),
                    GuildRankX = table.Column<int>(type: "integer", nullable: false),
                    GuildRankY = table.Column<int>(type: "integer", nullable: false),
                    ShowGuildRank = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateGuild", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateUser",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TextColor = table.Column<string>(type: "text", nullable: true),
                    FontSize = table.Column<int>(type: "integer", nullable: false),
                    TextX = table.Column<int>(type: "integer", nullable: false),
                    TextY = table.Column<int>(type: "integer", nullable: false),
                    ShowText = table.Column<bool>(type: "boolean", nullable: false),
                    IconX = table.Column<int>(type: "integer", nullable: false),
                    IconY = table.Column<int>(type: "integer", nullable: false),
                    IconSizeX = table.Column<int>(type: "integer", nullable: false),
                    IconSizeY = table.Column<int>(type: "integer", nullable: false),
                    ShowIcon = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketPanels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageJson = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketPanels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoleStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: true),
                    SavedRoles = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserXpStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Xp = table.Column<int>(type: "integer", nullable: false),
                    AwardedXp = table.Column<int>(type: "integer", nullable: false),
                    NotifyOnLevelUp = table.Column<int>(type: "integer", nullable: false),
                    LastLevelUp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValue: new DateTime(2017, 9, 21, 20, 53, 13, 307, DateTimeKind.Local)),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserXpStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoteRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Timer = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoteRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    BotId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warnings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Forgiven = table.Column<bool>(type: "boolean", nullable: false),
                    ForgivenBy = table.Column<string>(type: "text", nullable: true),
                    Moderator = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warnings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warnings2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Forgiven = table.Column<bool>(type: "boolean", nullable: false),
                    ForgivenBy = table.Column<string>(type: "text", nullable: true),
                    Moderator = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warnings2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuildConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Prefix = table.Column<string>(type: "text", nullable: true),
                    StaffRole = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GameMasterRole = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UseMessageCount = table.Column<bool>(type: "boolean", nullable: false),
                    MinMessageLength = table.Column<int>(type: "integer", nullable: false),
                    CommandLogChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DeleteMessageOnCommand = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    WarnMessage = table.Column<string>(type: "text", nullable: true),
                    AutoAssignRoleId = table.Column<string>(type: "text", nullable: true),
                    XpImgUrl = table.Column<string>(type: "text", nullable: true),
                    StatsOptOut = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CurrencyName = table.Column<string>(type: "text", nullable: true),
                    CurrencyEmoji = table.Column<string>(type: "text", nullable: true),
                    RewardAmount = table.Column<int>(type: "integer", nullable: false),
                    RewardTimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    GiveawayBanner = table.Column<string>(type: "text", nullable: true),
                    GiveawayEmbedColor = table.Column<string>(type: "text", nullable: true),
                    GiveawayWinEmbedColor = table.Column<string>(type: "text", nullable: true),
                    DmOnGiveawayWin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    GiveawayEndMessage = table.Column<string>(type: "text", nullable: true),
                    GiveawayPingRole = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StarboardAllowBots = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StarboardRemoveOnDelete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StarboardRemoveOnReactionsClear = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StarboardRemoveOnBelowThreshold = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UseStarboardBlacklist = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    StarboardCheckChannels = table.Column<string>(type: "text", nullable: true),
                    VotesPassword = table.Column<string>(type: "text", nullable: true),
                    VotesChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    VoteEmbed = table.Column<string>(type: "text", nullable: true),
                    SuggestionThreadType = table.Column<int>(type: "integer", nullable: false),
                    ArchiveOnDeny = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ArchiveOnAccept = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ArchiveOnConsider = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ArchiveOnImplement = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SuggestButtonMessage = table.Column<string>(type: "text", nullable: true),
                    SuggestButtonName = table.Column<string>(type: "text", nullable: true),
                    SuggestButtonEmote = table.Column<string>(type: "text", nullable: true),
                    ButtonRepostThreshold = table.Column<int>(type: "integer", nullable: false),
                    SuggestCommandsType = table.Column<int>(type: "integer", nullable: false),
                    AcceptChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DenyChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ConsiderChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ImplementChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EmoteMode = table.Column<int>(type: "integer", nullable: false),
                    SuggestMessage = table.Column<string>(type: "text", nullable: true),
                    DenyMessage = table.Column<string>(type: "text", nullable: true),
                    AcceptMessage = table.Column<string>(type: "text", nullable: true),
                    ImplementMessage = table.Column<string>(type: "text", nullable: true),
                    ConsiderMessage = table.Column<string>(type: "text", nullable: true),
                    MinSuggestLength = table.Column<int>(type: "integer", nullable: false),
                    MaxSuggestLength = table.Column<int>(type: "integer", nullable: false),
                    SuggestEmotes = table.Column<string>(type: "text", nullable: true),
                    sugnum = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    sugchan = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SuggestButtonChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Emote1Style = table.Column<int>(type: "integer", nullable: false),
                    Emote2Style = table.Column<int>(type: "integer", nullable: false),
                    Emote3Style = table.Column<int>(type: "integer", nullable: false),
                    Emote4Style = table.Column<int>(type: "integer", nullable: false),
                    Emote5Style = table.Column<int>(type: "integer", nullable: false),
                    SuggestButtonMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SuggestButtonRepostThreshold = table.Column<int>(type: "integer", nullable: false),
                    SuggestButtonColor = table.Column<int>(type: "integer", nullable: false),
                    AfkMessage = table.Column<string>(type: "text", nullable: true),
                    AutoBotRoleIds = table.Column<string>(type: "text", nullable: true),
                    GBEnabled = table.Column<int>(type: "integer", nullable: false),
                    GBAction = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConfessionLogChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ConfessionChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ConfessionBlacklist = table.Column<string>(type: "text", nullable: true),
                    MultiGreetType = table.Column<int>(type: "integer", nullable: false),
                    MemberRole = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TOpenMessage = table.Column<string>(type: "text", nullable: true),
                    GStartMessage = table.Column<string>(type: "text", nullable: true),
                    GEndMessage = table.Column<string>(type: "text", nullable: true),
                    GWinMessage = table.Column<string>(type: "text", nullable: true),
                    WarnlogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MiniWarnlogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SendBoostMessage = table.Column<bool>(type: "boolean", nullable: false),
                    GRolesBlacklist = table.Column<string>(type: "text", nullable: true),
                    GUsersBlacklist = table.Column<string>(type: "text", nullable: true),
                    BoostMessage = table.Column<string>(type: "text", nullable: true),
                    BoostMessageChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    BoostMessageDeleteAfter = table.Column<int>(type: "integer", nullable: false),
                    GiveawayEmote = table.Column<string>(type: "text", nullable: true),
                    TicketChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TicketCategory = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    snipeset = table.Column<bool>(type: "boolean", nullable: false),
                    AfkLength = table.Column<int>(type: "integer", nullable: false),
                    XpTxtTimeout = table.Column<int>(type: "integer", nullable: false),
                    XpTxtRate = table.Column<int>(type: "integer", nullable: false),
                    XpVoiceRate = table.Column<int>(type: "integer", nullable: false),
                    XpVoiceTimeout = table.Column<int>(type: "integer", nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    AfkType = table.Column<int>(type: "integer", nullable: false),
                    AfkDisabledChannels = table.Column<string>(type: "text", nullable: true),
                    AfkDel = table.Column<string>(type: "text", nullable: true),
                    AfkTimeout = table.Column<int>(type: "integer", nullable: false),
                    Joins = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Leaves = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Star2 = table.Column<string>(type: "text", nullable: true),
                    StarboardChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RepostThreshold = table.Column<int>(type: "integer", nullable: false),
                    PreviewLinks = table.Column<int>(type: "integer", nullable: false),
                    ReactChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    fwarn = table.Column<int>(type: "integer", nullable: false),
                    invwarn = table.Column<int>(type: "integer", nullable: false),
                    removeroles = table.Column<int>(type: "integer", nullable: false),
                    AutoDeleteGreetMessages = table.Column<bool>(type: "boolean", nullable: false),
                    AutoDeleteByeMessages = table.Column<bool>(type: "boolean", nullable: false),
                    AutoDeleteGreetMessagesTimer = table.Column<int>(type: "integer", nullable: false),
                    AutoDeleteByeMessagesTimer = table.Column<int>(type: "integer", nullable: false),
                    GreetMessageChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ByeMessageChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GreetHook = table.Column<string>(type: "text", nullable: true),
                    LeaveHook = table.Column<string>(type: "text", nullable: true),
                    SendDmGreetMessage = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DmGreetMessageText = table.Column<string>(type: "text", nullable: true),
                    SendChannelGreetMessage = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ChannelGreetMessageText = table.Column<string>(type: "text", nullable: true),
                    SendChannelByeMessage = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ChannelByeMessageText = table.Column<string>(type: "text", nullable: true),
                    ExclusiveSelfAssignedRoles = table.Column<bool>(type: "boolean", nullable: false),
                    AutoDeleteSelfAssignedRoleMessages = table.Column<bool>(type: "boolean", nullable: false),
                    LogSettingId = table.Column<int>(type: "integer", nullable: true),
                    VerbosePermissions = table.Column<bool>(type: "boolean", nullable: false),
                    PermissionRole = table.Column<string>(type: "text", nullable: true),
                    FilterInvites = table.Column<bool>(type: "boolean", nullable: false),
                    FilterLinks = table.Column<bool>(type: "boolean", nullable: false),
                    FilterWords = table.Column<bool>(type: "boolean", nullable: false),
                    MuteRoleName = table.Column<string>(type: "text", nullable: true),
                    CleverbotChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Locale = table.Column<string>(type: "text", nullable: true),
                    TimeZoneId = table.Column<string>(type: "text", nullable: true),
                    WarningsInitialized = table.Column<bool>(type: "boolean", nullable: false),
                    GameVoiceChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    VerboseErrors = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyStreamOffline = table.Column<bool>(type: "boolean", nullable: false),
                    WarnExpireHours = table.Column<int>(type: "integer", nullable: false),
                    WarnExpireAction = table.Column<int>(type: "integer", nullable: false),
                    JoinGraphColor = table.Column<long>(type: "bigint", nullable: false),
                    LeaveGraphColor = table.Column<long>(type: "bigint", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildConfigs_LogSettings_LogSettingId",
                        column: x => x.LogSettingId,
                        principalTable: "LogSettings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "IgnoredLogChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogSettingId = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredLogChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IgnoredLogChannels_LogSettings_LogSettingId",
                        column: x => x.LogSettingId,
                        principalTable: "LogSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistSong",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MusicPlaylistId = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Uri = table.Column<string>(type: "text", nullable: true),
                    Query = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistSong", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaylistSong_MusicPlaylists_MusicPlaylistId",
                        column: x => x.MusicPlaylistId,
                        principalTable: "MusicPlaylists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PollAnswer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Text = table.Column<string>(type: "text", nullable: true),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    PollsId = table.Column<int>(type: "integer", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollAnswer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollAnswer_Poll_PollsId",
                        column: x => x.PollsId,
                        principalTable: "Poll",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PollVote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    VoteIndex = table.Column<int>(type: "integer", nullable: false),
                    PollId = table.Column<int>(type: "integer", nullable: false),
                    PollsId = table.Column<int>(type: "integer", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollVote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollVote_Poll_PollsId",
                        column: x => x.PollsId,
                        principalTable: "Poll",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Template",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OutputSizeX = table.Column<int>(type: "integer", nullable: false),
                    OutputSizeY = table.Column<int>(type: "integer", nullable: false),
                    TimeOnLevelFormat = table.Column<string>(type: "text", nullable: true),
                    TimeOnLevelX = table.Column<int>(type: "integer", nullable: false),
                    TimeOnLevelY = table.Column<int>(type: "integer", nullable: false),
                    TimeOnLevelFontSize = table.Column<int>(type: "integer", nullable: false),
                    TimeOnLevelColor = table.Column<string>(type: "text", nullable: true),
                    ShowTimeOnLevel = table.Column<bool>(type: "boolean", nullable: false),
                    AwardedX = table.Column<int>(type: "integer", nullable: false),
                    AwardedY = table.Column<int>(type: "integer", nullable: false),
                    AwardedFontSize = table.Column<int>(type: "integer", nullable: false),
                    AwardedColor = table.Column<string>(type: "text", nullable: true),
                    ShowAwarded = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateUserId = table.Column<int>(type: "integer", nullable: false),
                    TemplateGuildId = table.Column<int>(type: "integer", nullable: false),
                    TemplateClubId = table.Column<int>(type: "integer", nullable: false),
                    TemplateBarId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Template", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Template_TemplateBar_TemplateBarId",
                        column: x => x.TemplateBarId,
                        principalTable: "TemplateBar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Template_TemplateClub_TemplateClubId",
                        column: x => x.TemplateClubId,
                        principalTable: "TemplateClub",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Template_TemplateGuild_TemplateGuildId",
                        column: x => x.TemplateGuildId,
                        principalTable: "TemplateGuild",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Template_TemplateUser_TemplateUserId",
                        column: x => x.TemplateUserId,
                        principalTable: "TemplateUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketButtons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketPanelId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Emoji = table.Column<string>(type: "text", nullable: false),
                    OpenMessage = table.Column<string>(type: "text", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketButtons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketButtons_TicketPanels_TicketPanelId",
                        column: x => x.TicketPanelId,
                        principalTable: "TicketPanels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AntiAltSetting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    MinAge = table.Column<string>(type: "text", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    ActionDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntiAltSetting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AntiAltSetting_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AntiRaidSetting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    UserThreshold = table.Column<int>(type: "integer", nullable: false),
                    Seconds = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    PunishDuration = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntiRaidSetting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AntiRaidSetting_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AntiSpamSetting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    MessageThreshold = table.Column<int>(type: "integer", nullable: false),
                    MuteTime = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntiSpamSetting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AntiSpamSetting_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommandAlias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Trigger = table.Column<string>(type: "text", nullable: true),
                    Mapping = table.Column<string>(type: "text", nullable: true),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandAlias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandAlias_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CommandCooldown",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Seconds = table.Column<int>(type: "integer", nullable: false),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    CommandName = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandCooldown", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandCooldown_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DelMsgOnCmdChannel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    State = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelMsgOnCmdChannel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DelMsgOnCmdChannel_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedSub",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedSub", x => x.Id);
                    table.UniqueConstraint("AK_FeedSub_GuildConfigId_Url", x => new { x.GuildConfigId, x.Url });
                    table.ForeignKey(
                        name: "FK_FeedSub_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FilteredWord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Word = table.Column<string>(type: "text", nullable: true),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilteredWord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilteredWord_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FilterInvitesChannelIds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilterInvitesChannelIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilterInvitesChannelIds_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FilterLinksChannelId",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilterLinksChannelId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilterLinksChannelId_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FilterWordsChannelIds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilterWordsChannelIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilterWordsChannelIds_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FollowedStream",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowedStream", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FollowedStream_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupName",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupName", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupName_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildRepeater",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LastMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Interval = table.Column<string>(type: "text", nullable: true),
                    StartTimeOfDay = table.Column<string>(type: "text", nullable: true),
                    NoRedundant = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildRepeater", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildRepeater_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MutedUserId",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    roles = table.Column<string>(type: "text", nullable: true),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MutedUserId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MutedUserId_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NsfwBlacklitedTag",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    Tag = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NsfwBlacklitedTag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NsfwBlacklitedTag_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Permissionv2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: true),
                    PrimaryTarget = table.Column<int>(type: "integer", nullable: false),
                    PrimaryTargetId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SecondaryTarget = table.Column<int>(type: "integer", nullable: false),
                    SecondaryTargetName = table.Column<string>(type: "text", nullable: true),
                    IsCustomCommand = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<bool>(type: "boolean", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissionv2", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permissionv2_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReactionRoleMessage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Exclusive = table.Column<bool>(type: "boolean", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionRoleMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReactionRoleMessage_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StreamRoleSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    AddRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    FromRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Keyword = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamRoleSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamRoleSettings_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnbanTimer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UnbanAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnbanTimer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnbanTimer_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnmuteTimer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UnmuteAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnmuteTimer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnmuteTimer_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnroleTimer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UnbanAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnroleTimer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnroleTimer_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VcRoleInfo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    VoiceChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VcRoleInfo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VcRoleInfo_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WarningPunishment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    Punishment = table.Column<int>(type: "integer", nullable: false),
                    Time = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarningPunishment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarningPunishment_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WarningPunishment2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    Punishment = table.Column<int>(type: "integer", nullable: false),
                    Time = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarningPunishment2", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarningPunishment2_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XpSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    XpRoleRewardExclusive = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyMessage = table.Column<string>(type: "text", nullable: true),
                    ServerExcluded = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpSettings_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AntiSpamIgnore",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    AntiSpamSettingId = table.Column<int>(type: "integer", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntiSpamIgnore", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AntiSpamIgnore_AntiSpamSetting_AntiSpamSettingId",
                        column: x => x.AntiSpamSettingId,
                        principalTable: "AntiSpamSetting",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReactionRole",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmoteName = table.Column<string>(type: "text", nullable: true),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ReactionRoleMessageId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReactionRole_ReactionRoleMessage_ReactionRoleMessageId",
                        column: x => x.ReactionRoleMessageId,
                        principalTable: "ReactionRoleMessage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StreamRoleBlacklistedUser",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    StreamRoleSettingsId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamRoleBlacklistedUser", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamRoleBlacklistedUser_StreamRoleSettings_StreamRoleSett~",
                        column: x => x.StreamRoleSettingsId,
                        principalTable: "StreamRoleSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StreamRoleWhitelistedUser",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StreamRoleSettingsId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamRoleWhitelistedUser", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamRoleWhitelistedUser_StreamRoleSettings_StreamRoleSett~",
                        column: x => x.StreamRoleSettingsId,
                        principalTable: "StreamRoleSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExcludedItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    XpSettingsId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcludedItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExcludedItem_XpSettings_XpSettingsId",
                        column: x => x.XpSettingsId,
                        principalTable: "XpSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XpCurrencyReward",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    XpSettingsId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpCurrencyReward", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpCurrencyReward_XpSettings_XpSettingsId",
                        column: x => x.XpSettingsId,
                        principalTable: "XpSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XpRoleReward",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    XpSettingsId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpRoleReward", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpRoleReward_XpSettings_XpSettingsId",
                        column: x => x.XpSettingsId,
                        principalTable: "XpSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AntiAltSetting_GuildConfigId",
                table: "AntiAltSetting",
                column: "GuildConfigId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntiRaidSetting_GuildConfigId",
                table: "AntiRaidSetting",
                column: "GuildConfigId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntiSpamIgnore_AntiSpamSettingId",
                table: "AntiSpamIgnore",
                column: "AntiSpamSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_AntiSpamSetting_GuildConfigId",
                table: "AntiSpamSetting",
                column: "GuildConfigId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BanTemplates_GuildId",
                table: "BanTemplates",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommandAlias_GuildConfigId",
                table: "CommandAlias",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandCooldown_GuildConfigId",
                table: "CommandCooldown",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_DelMsgOnCmdChannel_GuildConfigId",
                table: "DelMsgOnCmdChannel",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordPermOverrides_GuildId_Command",
                table: "DiscordPermOverrides",
                columns: new[] { "GuildId", "Command" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscordUser_TotalXp",
                table: "DiscordUser",
                column: "TotalXp");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordUser_UserId",
                table: "DiscordUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcludedItem_XpSettingsId",
                table: "ExcludedItem",
                column: "XpSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_FilteredWord_GuildConfigId",
                table: "FilteredWord",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_FilterInvitesChannelIds_GuildConfigId",
                table: "FilterInvitesChannelIds",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_FilterLinksChannelId_GuildConfigId",
                table: "FilterLinksChannelId",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_FilterWordsChannelIds_GuildConfigId",
                table: "FilterWordsChannelIds",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowedStream_GuildConfigId",
                table: "FollowedStream",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupName_GuildConfigId_Number",
                table: "GroupName",
                columns: new[] { "GuildConfigId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildConfigs_GuildId",
                table: "GuildConfigs",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildConfigs_LogSettingId",
                table: "GuildConfigs",
                column: "LogSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildConfigs_WarnExpireHours",
                table: "GuildConfigs",
                column: "WarnExpireHours");

            migrationBuilder.CreateIndex(
                name: "IX_GuildRepeater_GuildConfigId",
                table: "GuildRepeater",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredLogChannels_LogSettingId",
                table: "IgnoredLogChannels",
                column: "LogSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicPlayerSettings_GuildId",
                table: "MusicPlayerSettings",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MutedUserId_GuildConfigId",
                table: "MutedUserId",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_NsfwBlacklitedTag_GuildConfigId",
                table: "NsfwBlacklitedTag",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Permission_NextId",
                table: "Permission",
                column: "NextId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissionv2_GuildConfigId",
                table: "Permissionv2",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistSong_MusicPlaylistId",
                table: "PlaylistSong",
                column: "MusicPlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_Poll_GuildId",
                table: "Poll",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollAnswer_PollsId",
                table: "PollAnswer",
                column: "PollsId");

            migrationBuilder.CreateIndex(
                name: "IX_PollVote_PollsId",
                table: "PollVote",
                column: "PollsId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_GuildId",
                table: "Quotes",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Keyword",
                table: "Quotes",
                column: "Keyword");

            migrationBuilder.CreateIndex(
                name: "IX_ReactionRole_ReactionRoleMessageId",
                table: "ReactionRole",
                column: "ReactionRoleMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ReactionRoleMessage_GuildConfigId",
                table: "ReactionRoleMessage",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_When",
                table: "Reminders",
                column: "When");

            migrationBuilder.CreateIndex(
                name: "IX_SelfAssignableRoles_GuildId_RoleId",
                table: "SelfAssignableRoles",
                columns: new[] { "GuildId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamRoleBlacklistedUser_StreamRoleSettingsId",
                table: "StreamRoleBlacklistedUser",
                column: "StreamRoleSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamRoleSettings_GuildConfigId",
                table: "StreamRoleSettings",
                column: "GuildConfigId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamRoleWhitelistedUser_StreamRoleSettingsId",
                table: "StreamRoleWhitelistedUser",
                column: "StreamRoleSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateBarId",
                table: "Template",
                column: "TemplateBarId");

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateClubId",
                table: "Template",
                column: "TemplateClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateGuildId",
                table: "Template",
                column: "TemplateGuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateUserId",
                table: "Template",
                column: "TemplateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketButtons_TicketPanelId",
                table: "TicketButtons",
                column: "TicketPanelId");

            migrationBuilder.CreateIndex(
                name: "IX_UnbanTimer_GuildConfigId",
                table: "UnbanTimer",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_UnmuteTimer_GuildConfigId",
                table: "UnmuteTimer",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_UnroleTimer_GuildConfigId",
                table: "UnroleTimer",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_AwardedXp",
                table: "UserXpStats",
                column: "AwardedXp");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_GuildId",
                table: "UserXpStats",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_UserId",
                table: "UserXpStats",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_UserId_GuildId",
                table: "UserXpStats",
                columns: new[] { "UserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_Xp",
                table: "UserXpStats",
                column: "Xp");

            migrationBuilder.CreateIndex(
                name: "IX_VcRoleInfo_GuildConfigId",
                table: "VcRoleInfo",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_WarningPunishment_GuildConfigId",
                table: "WarningPunishment",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_WarningPunishment2_GuildConfigId",
                table: "WarningPunishment2",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_DateAdded",
                table: "Warnings",
                column: "DateAdded");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_GuildId",
                table: "Warnings",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_UserId",
                table: "Warnings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_XpCurrencyReward_XpSettingsId",
                table: "XpCurrencyReward",
                column: "XpSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_XpRoleReward_XpSettingsId_Level",
                table: "XpRoleReward",
                columns: new[] { "XpSettingsId", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XpSettings_GuildConfigId",
                table: "XpSettings",
                column: "GuildConfigId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AFK");

            migrationBuilder.DropTable(
                name: "AntiAltSetting");

            migrationBuilder.DropTable(
                name: "AntiRaidSetting");

            migrationBuilder.DropTable(
                name: "AntiSpamIgnore");

            migrationBuilder.DropTable(
                name: "AuthCodes");

            migrationBuilder.DropTable(
                name: "AutoBanRoles");

            migrationBuilder.DropTable(
                name: "AutoBanWords");

            migrationBuilder.DropTable(
                name: "AutoCommands");

            migrationBuilder.DropTable(
                name: "AutoPublish");

            migrationBuilder.DropTable(
                name: "BanTemplates");

            migrationBuilder.DropTable(
                name: "Blacklist");

            migrationBuilder.DropTable(
                name: "ChatTriggers");

            migrationBuilder.DropTable(
                name: "CommandAlias");

            migrationBuilder.DropTable(
                name: "CommandCooldown");

            migrationBuilder.DropTable(
                name: "CommandStats");

            migrationBuilder.DropTable(
                name: "Confessions");

            migrationBuilder.DropTable(
                name: "DelMsgOnCmdChannel");

            migrationBuilder.DropTable(
                name: "DiscordPermOverrides");

            migrationBuilder.DropTable(
                name: "DiscordUser");

            migrationBuilder.DropTable(
                name: "ExcludedItem");

            migrationBuilder.DropTable(
                name: "FeedSub");

            migrationBuilder.DropTable(
                name: "FilteredWord");

            migrationBuilder.DropTable(
                name: "FilterInvitesChannelIds");

            migrationBuilder.DropTable(
                name: "FilterLinksChannelId");

            migrationBuilder.DropTable(
                name: "FilterWordsChannelIds");

            migrationBuilder.DropTable(
                name: "FollowedStream");

            migrationBuilder.DropTable(
                name: "Giveaways");

            migrationBuilder.DropTable(
                name: "GiveawayUsers");

            migrationBuilder.DropTable(
                name: "GlobalUserBalance");

            migrationBuilder.DropTable(
                name: "GroupName");

            migrationBuilder.DropTable(
                name: "GuildRepeater");

            migrationBuilder.DropTable(
                name: "GuildUserBalance");

            migrationBuilder.DropTable(
                name: "Highlights");

            migrationBuilder.DropTable(
                name: "HighlightSettings");

            migrationBuilder.DropTable(
                name: "IgnoredLogChannels");

            migrationBuilder.DropTable(
                name: "JoinLeaveLogs");

            migrationBuilder.DropTable(
                name: "MessageCounts");

            migrationBuilder.DropTable(
                name: "MultiGreets");

            migrationBuilder.DropTable(
                name: "MusicPlayerSettings");

            migrationBuilder.DropTable(
                name: "MutedUserId");

            migrationBuilder.DropTable(
                name: "NsfwBlacklitedTag");

            migrationBuilder.DropTable(
                name: "OwnerOnly");

            migrationBuilder.DropTable(
                name: "Permission");

            migrationBuilder.DropTable(
                name: "Permissionv2");

            migrationBuilder.DropTable(
                name: "PlaylistSong");

            migrationBuilder.DropTable(
                name: "PollAnswer");

            migrationBuilder.DropTable(
                name: "PollVote");

            migrationBuilder.DropTable(
                name: "PublishUserBlacklist");

            migrationBuilder.DropTable(
                name: "PublishWordBlacklist");

            migrationBuilder.DropTable(
                name: "Quotes");

            migrationBuilder.DropTable(
                name: "ReactionRole");

            migrationBuilder.DropTable(
                name: "Reminders");

            migrationBuilder.DropTable(
                name: "RoleGreets");

            migrationBuilder.DropTable(
                name: "RoleStateSettings");

            migrationBuilder.DropTable(
                name: "RotatingStatus");

            migrationBuilder.DropTable(
                name: "SelfAssignableRoles");

            migrationBuilder.DropTable(
                name: "ServerRecoveryStore");

            migrationBuilder.DropTable(
                name: "Starboard");

            migrationBuilder.DropTable(
                name: "StatusRoles");

            migrationBuilder.DropTable(
                name: "StreamRoleBlacklistedUser");

            migrationBuilder.DropTable(
                name: "StreamRoleWhitelistedUser");

            migrationBuilder.DropTable(
                name: "Suggestions");

            migrationBuilder.DropTable(
                name: "SuggestThreads");

            migrationBuilder.DropTable(
                name: "SuggestVotes");

            migrationBuilder.DropTable(
                name: "Template");

            migrationBuilder.DropTable(
                name: "TicketButtons");

            migrationBuilder.DropTable(
                name: "TransactionHistory");

            migrationBuilder.DropTable(
                name: "UnbanTimer");

            migrationBuilder.DropTable(
                name: "UnmuteTimer");

            migrationBuilder.DropTable(
                name: "UnroleTimer");

            migrationBuilder.DropTable(
                name: "UserRoleStates");

            migrationBuilder.DropTable(
                name: "UserXpStats");

            migrationBuilder.DropTable(
                name: "VcRoleInfo");

            migrationBuilder.DropTable(
                name: "VoteRoles");

            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "WarningPunishment");

            migrationBuilder.DropTable(
                name: "WarningPunishment2");

            migrationBuilder.DropTable(
                name: "Warnings");

            migrationBuilder.DropTable(
                name: "Warnings2");

            migrationBuilder.DropTable(
                name: "XpCurrencyReward");

            migrationBuilder.DropTable(
                name: "XpRoleReward");

            migrationBuilder.DropTable(
                name: "AntiSpamSetting");

            migrationBuilder.DropTable(
                name: "MusicPlaylists");

            migrationBuilder.DropTable(
                name: "Poll");

            migrationBuilder.DropTable(
                name: "ReactionRoleMessage");

            migrationBuilder.DropTable(
                name: "StreamRoleSettings");

            migrationBuilder.DropTable(
                name: "TemplateBar");

            migrationBuilder.DropTable(
                name: "TemplateClub");

            migrationBuilder.DropTable(
                name: "TemplateGuild");

            migrationBuilder.DropTable(
                name: "TemplateUser");

            migrationBuilder.DropTable(
                name: "TicketPanels");

            migrationBuilder.DropTable(
                name: "XpSettings");

            migrationBuilder.DropTable(
                name: "GuildConfigs");

            migrationBuilder.DropTable(
                name: "LogSettings");
        }
    }
}
