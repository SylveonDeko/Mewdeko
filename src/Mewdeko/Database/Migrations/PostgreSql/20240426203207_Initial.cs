#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "AFK",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Message = table.Column<string>("text", nullable: true),
                WasTimed = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                When = table.Column<DateTime>("timestamp without time zone", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AFK", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "AuthCodes",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Scopes = table.Column<string>("text", nullable: true),
                Token = table.Column<string>("text", nullable: true),
                RefreshToken = table.Column<string>("text", nullable: true),
                ExpiresAt = table.Column<DateTime>("timestamp without time zone", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthCodes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "AutoBanRoles",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoBanRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "AutoBanWords",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Word = table.Column<string>("text", nullable: true),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoBanWords", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "AutoCommands",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CommandText = table.Column<string>("text", nullable: true),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelName = table.Column<string>("text", nullable: true),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: true),
                GuildName = table.Column<string>("text", nullable: true),
                VoiceChannelId = table.Column<decimal>("numeric(20,0)", nullable: true),
                VoiceChannelName = table.Column<string>("text", nullable: true),
                Interval = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoCommands", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "AutoPublish",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                BlacklistedUsers = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoPublish", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "BanTemplates",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Text = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BanTemplates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Blacklist",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ItemId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Type = table.Column<int>("integer", nullable: false),
                Reason = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Blacklist", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "ChatTriggers",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UseCount = table.Column<decimal>("numeric(20,0)", nullable: false),
                IsRegex = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                OwnerOnly = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: true),
                Response = table.Column<string>("text", nullable: true),
                Trigger = table.Column<string>("text", nullable: true),
                PrefixType = table.Column<int>("integer", nullable: false),
                CustomPrefix = table.Column<string>("text", nullable: true),
                AutoDeleteTrigger = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                ReactToTrigger = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                NoRespond = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                DmResponse = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                ContainsAnywhere = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                AllowTarget = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                Reactions = table.Column<string>("text", nullable: true),
                GrantedRoles = table.Column<string>("text", nullable: true),
                RemovedRoles = table.Column<string>("text", nullable: true),
                RoleGrantType = table.Column<int>("integer", nullable: false),
                ValidTriggerTypes = table.Column<int>("integer", nullable: false),
                ApplicationCommandId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ApplicationCommandName = table.Column<string>("text", nullable: true),
                ApplicationCommandDescription = table.Column<string>("text", nullable: true),
                ApplicationCommandType = table.Column<int>("integer", nullable: false),
                EphemeralResponse = table.Column<bool>("boolean", nullable: false),
                CrosspostingChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                CrosspostingWebhookUrl = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatTriggers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "CommandStats",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                NameOrId = table.Column<string>("text", nullable: false),
                Module = table.Column<string>("text", nullable: true, defaultValue: ""),
                IsSlash = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                Trigger = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CommandStats", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Confessions",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                MessageId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ConfessNumber = table.Column<decimal>("numeric(20,0)", nullable: false),
                Confession = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Confessions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "DiscordPermOverrides",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Perm = table.Column<decimal>("numeric(20,0)", nullable: false),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: true),
                Command = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DiscordPermOverrides", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "DiscordUser",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Username = table.Column<string>("text", nullable: true),
                Discriminator = table.Column<string>("text", nullable: true),
                AvatarId = table.Column<string>("text", nullable: true),
                IsClubAdmin = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                TotalXp = table.Column<int>("integer", nullable: false),
                LastLevelUp =
                    table.Column<DateTime>("timestamp without time zone", nullable: true,
                        defaultValue: new DateTime(2017, 9, 21, 20, 53, 13, 305, DateTimeKind.Local)),
                NotifyOnLevelUp = table.Column<int>("integer", nullable: false, defaultValue: 0),
                IsDragon = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                Pronouns = table.Column<string>("text", nullable: true),
                PronounsClearedReason = table.Column<string>("text", nullable: true),
                PronounsDisabled = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                Bio = table.Column<string>("text", nullable: true),
                ProfileImageUrl = table.Column<string>("text", nullable: true),
                ZodiacSign = table.Column<string>("text", nullable: true),
                ProfilePrivacy = table.Column<int>("integer", nullable: false, defaultValue: 0),
                ProfileColor = table.Column<long>("bigint", nullable: true),
                Birthday = table.Column<DateTime>("timestamp without time zone", nullable: true),
                SwitchFriendCode = table.Column<string>("text", nullable: true),
                BirthdayDisplayMode = table.Column<int>("integer", nullable: false, defaultValue: 0),
                StatsOptOut = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DiscordUser", x => x.Id);
                table.UniqueConstraint("AK_DiscordUser_UserId", x => x.UserId);
            });

        migrationBuilder.CreateTable(
            "Giveaways",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                When = table.Column<DateTime>("timestamp without time zone", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ServerId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Ended = table.Column<int>("integer", nullable: false),
                MessageId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Winners = table.Column<int>("integer", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Item = table.Column<string>("text", nullable: true),
                RestrictTo = table.Column<string>("text", nullable: true),
                BlacklistUsers = table.Column<string>("text", nullable: true),
                BlacklistRoles = table.Column<string>("text", nullable: true),
                Emote = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Giveaways", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "GlobalUserBalance",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Balance = table.Column<long>("bigint", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GlobalUserBalance", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "GuildUserBalance",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Balance = table.Column<long>("bigint", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GuildUserBalance", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Highlights",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Word = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Highlights", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "HighlightSettings",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                IgnoredChannels = table.Column<string>("text", nullable: true),
                IgnoredUsers = table.Column<string>("text", nullable: true),
                HighlightsOn = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HighlightSettings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "JoinLeaveLogs",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                IsJoin = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JoinLeaveLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "LogSettings",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                LogOtherId = table.Column<decimal>("numeric(20,0)", nullable: true),
                MessageUpdatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                MessageDeletedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                ThreadCreatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                ThreadDeletedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                ThreadUpdatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                UsernameUpdatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                NicknameUpdatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                AvatarUpdatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                UserLeftId = table.Column<decimal>("numeric(20,0)", nullable: true),
                UserBannedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                UserUnbannedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                UserUpdatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                UserJoinedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                UserRoleAddedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                UserRoleRemovedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                UserMutedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                LogUserPresenceId = table.Column<decimal>("numeric(20,0)", nullable: true),
                LogVoicePresenceId = table.Column<decimal>("numeric(20,0)", nullable: true),
                LogVoicePresenceTTSId = table.Column<decimal>("numeric(20,0)", nullable: true),
                ServerUpdatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                RoleUpdatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                RoleDeletedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                EventCreatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                RoleCreatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                ChannelCreatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                ChannelDestroyedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                ChannelUpdatedId = table.Column<decimal>("numeric(20,0)", nullable: true),
                IsLogging = table.Column<long>("bigint", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                MessageUpdated = table.Column<long>("bigint", nullable: false),
                MessageDeleted = table.Column<long>("bigint", nullable: false),
                UserJoined = table.Column<long>("bigint", nullable: false),
                UserLeft = table.Column<long>("bigint", nullable: false),
                UserBanned = table.Column<long>("bigint", nullable: false),
                UserUnbanned = table.Column<long>("bigint", nullable: false),
                UserUpdated = table.Column<long>("bigint", nullable: false),
                ChannelCreated = table.Column<long>("bigint", nullable: false),
                ChannelDestroyed = table.Column<long>("bigint", nullable: false),
                ChannelUpdated = table.Column<long>("bigint", nullable: false),
                LogUserPresence = table.Column<long>("bigint", nullable: false),
                UserPresenceChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                LogVoicePresence = table.Column<long>("bigint", nullable: false),
                VoicePresenceChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LogSettings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "MultiGreets",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Message = table.Column<string>("text", nullable: false),
                GreetBots = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                DeleteTime = table.Column<int>("integer", nullable: false, defaultValue: 1),
                WebhookUrl = table.Column<string>("text", nullable: true),
                Disabled = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MultiGreets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "MusicPlayerSettings",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                PlayerRepeat = table.Column<int>("integer", nullable: false),
                MusicChannelId = table.Column<decimal>("numeric(20,0)", nullable: true),
                Volume = table.Column<int>("integer", nullable: false, defaultValue: 100),
                AutoDisconnect = table.Column<int>("integer", nullable: false),
                AutoPlay = table.Column<int>("integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MusicPlayerSettings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "MusicPlaylists",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>("text", nullable: true),
                Author = table.Column<string>("text", nullable: true),
                AuthorId = table.Column<decimal>("numeric(20,0)", nullable: false),
                IsDefault = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MusicPlaylists", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "OwnerOnly",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Owners = table.Column<string>("text", nullable: true),
                GptTokensUsed = table.Column<int>("integer", nullable: false),
                CurrencyEmote = table.Column<string>("text", nullable: true),
                RewardAmount = table.Column<int>("integer", nullable: false),
                RewardTimeoutSeconds = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OwnerOnly", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Permission",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                NextId = table.Column<int>("integer", nullable: true),
                PrimaryTarget = table.Column<int>("integer", nullable: false),
                PrimaryTargetId = table.Column<decimal>("numeric(20,0)", nullable: false),
                SecondaryTarget = table.Column<int>("integer", nullable: false),
                SecondaryTargetName = table.Column<string>("text", nullable: true),
                State = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Permission", x => x.Id);
                table.ForeignKey(
                    "FK_Permission_Permission_NextId",
                    x => x.NextId,
                    "Permission",
                    "Id");
            });

        migrationBuilder.CreateTable(
            "Poll",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Question = table.Column<string>("text", nullable: true),
                PollType = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Poll", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "PublishUserBlacklist",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                User = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublishUserBlacklist", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "PublishWordBlacklist",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Word = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublishWordBlacklist", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Quotes",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Keyword = table.Column<string>("text", nullable: false),
                AuthorName = table.Column<string>("text", nullable: false),
                AuthorId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Text = table.Column<string>("text", nullable: false),
                UseCount = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Quotes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Reminders",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                When = table.Column<DateTime>("timestamp without time zone", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ServerId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Message = table.Column<string>("text", nullable: true),
                IsPrivate = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Reminders", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "RoleGreets",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                GreetBots = table.Column<bool>("boolean", nullable: false),
                Message = table.Column<string>("text", nullable: true),
                DeleteTime = table.Column<int>("integer", nullable: false),
                WebhookUrl = table.Column<string>("text", nullable: true),
                Disabled = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleGreets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "RoleStateSettings",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Enabled = table.Column<bool>("boolean", nullable: false),
                ClearOnBan = table.Column<bool>("boolean", nullable: false),
                IgnoreBots = table.Column<bool>("boolean", nullable: false),
                DeniedRoles = table.Column<string>("text", nullable: true),
                DeniedUsers = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleStateSettings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "RotatingStatus",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Status = table.Column<string>("text", nullable: true),
                Type = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RotatingStatus", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "SelfAssignableRoles",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Group = table.Column<int>("integer", nullable: false, defaultValue: 0),
                LevelRequirement = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SelfAssignableRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "ServerRecoveryStore",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                RecoveryKey = table.Column<string>("text", nullable: true),
                TwoFactorKey = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServerRecoveryStore", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Starboard",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                MessageId = table.Column<decimal>("numeric(20,0)", nullable: false),
                PostId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Starboard", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "StatusRoles",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Status = table.Column<string>("text", nullable: true),
                ToAdd = table.Column<string>("text", nullable: true),
                ToRemove = table.Column<string>("text", nullable: true),
                StatusEmbed = table.Column<string>("text", nullable: true),
                ReaddRemoved = table.Column<bool>("boolean", nullable: false),
                RemoveAdded = table.Column<bool>("boolean", nullable: false),
                StatusChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Suggestions",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                SuggestionId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Suggestion = table.Column<string>("text", nullable: true),
                MessageId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                EmoteCount1 = table.Column<int>("integer", nullable: false),
                EmoteCount2 = table.Column<int>("integer", nullable: false),
                EmoteCount3 = table.Column<int>("integer", nullable: false),
                EmoteCount4 = table.Column<int>("integer", nullable: false),
                EmoteCount5 = table.Column<int>("integer", nullable: false),
                StateChangeUser = table.Column<decimal>("numeric(20,0)", nullable: false),
                StateChangeCount = table.Column<decimal>("numeric(20,0)", nullable: false),
                StateChangeMessageId = table.Column<decimal>("numeric(20,0)", nullable: false),
                CurrentState = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Suggestions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "SuggestThreads",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                MessageId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ThreadChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SuggestThreads", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "SuggestVotes",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                MessageId = table.Column<decimal>("numeric(20,0)", nullable: false),
                EmotePicked = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SuggestVotes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "TemplateBar",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                BarColor = table.Column<string>("text", nullable: true),
                BarPointAx = table.Column<int>("integer", nullable: false),
                BarPointAy = table.Column<int>("integer", nullable: false),
                BarPointBx = table.Column<int>("integer", nullable: false),
                BarPointBy = table.Column<int>("integer", nullable: false),
                BarLength = table.Column<int>("integer", nullable: false),
                BarTransparency = table.Column<byte>("smallint", nullable: false),
                BarDirection = table.Column<int>("integer", nullable: false),
                ShowBar = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateBar", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "TemplateClub",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ClubIconX = table.Column<int>("integer", nullable: false),
                ClubIconY = table.Column<int>("integer", nullable: false),
                ClubIconSizeX = table.Column<int>("integer", nullable: false),
                ClubIconSizeY = table.Column<int>("integer", nullable: false),
                ShowClubIcon = table.Column<bool>("boolean", nullable: false),
                ClubNameColor = table.Column<string>("text", nullable: true),
                ClubNameFontSize = table.Column<int>("integer", nullable: false),
                ClubNameX = table.Column<int>("integer", nullable: false),
                ClubNameY = table.Column<int>("integer", nullable: false),
                ShowClubName = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateClub", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "TemplateGuild",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildLevelColor = table.Column<string>("text", nullable: true),
                GuildLevelFontSize = table.Column<int>("integer", nullable: false),
                GuildLevelX = table.Column<int>("integer", nullable: false),
                GuildLevelY = table.Column<int>("integer", nullable: false),
                ShowGuildLevel = table.Column<bool>("boolean", nullable: false),
                GuildRankColor = table.Column<string>("text", nullable: true),
                GuildRankFontSize = table.Column<int>("integer", nullable: false),
                GuildRankX = table.Column<int>("integer", nullable: false),
                GuildRankY = table.Column<int>("integer", nullable: false),
                ShowGuildRank = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateGuild", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "TemplateUser",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TextColor = table.Column<string>("text", nullable: true),
                FontSize = table.Column<int>("integer", nullable: false),
                TextX = table.Column<int>("integer", nullable: false),
                TextY = table.Column<int>("integer", nullable: false),
                ShowText = table.Column<bool>("boolean", nullable: false),
                IconX = table.Column<int>("integer", nullable: false),
                IconY = table.Column<int>("integer", nullable: false),
                IconSizeX = table.Column<int>("integer", nullable: false),
                IconSizeY = table.Column<int>("integer", nullable: false),
                ShowIcon = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateUser", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "TransactionHistory",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: true),
                Amount = table.Column<long>("bigint", nullable: false),
                Description = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransactionHistory", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "UserRoleStates",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserName = table.Column<string>("text", nullable: true),
                SavedRoles = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoleStates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "UserXpStats",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Xp = table.Column<int>("integer", nullable: false),
                AwardedXp = table.Column<int>("integer", nullable: false),
                NotifyOnLevelUp = table.Column<int>("integer", nullable: false),
                LastLevelUp =
                    table.Column<DateTime>("timestamp without time zone", nullable: false,
                        defaultValue: new DateTime(2017, 9, 21, 20, 53, 13, 307, DateTimeKind.Local)),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserXpStats", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "VoteRoles",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Timer = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VoteRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Votes",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                BotId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Votes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Warnings",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Reason = table.Column<string>("text", nullable: true),
                Forgiven = table.Column<bool>("boolean", nullable: false),
                ForgivenBy = table.Column<string>("text", nullable: true),
                Moderator = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Warnings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "Warnings2",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Reason = table.Column<string>("text", nullable: true),
                Forgiven = table.Column<bool>("boolean", nullable: false),
                ForgivenBy = table.Column<string>("text", nullable: true),
                Moderator = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Warnings2", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "GuildConfigs",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Prefix = table.Column<string>("text", nullable: true),
                StaffRole = table.Column<decimal>("numeric(20,0)", nullable: false),
                GameMasterRole = table.Column<decimal>("numeric(20,0)", nullable: false),
                CommandLogChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                DeleteMessageOnCommand = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                WarnMessage = table.Column<string>("text", nullable: true),
                AutoAssignRoleId = table.Column<string>("text", nullable: true),
                XpImgUrl = table.Column<string>("text", nullable: true),
                StatsOptOut = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                CurrencyName = table.Column<string>("text", nullable: true),
                CurrencyEmoji = table.Column<string>("text", nullable: true),
                RewardAmount = table.Column<int>("integer", nullable: false),
                RewardTimeoutSeconds = table.Column<int>("integer", nullable: false),
                GiveawayBanner = table.Column<string>("text", nullable: true),
                GiveawayEmbedColor = table.Column<string>("text", nullable: true),
                GiveawayWinEmbedColor = table.Column<string>("text", nullable: true),
                DmOnGiveawayWin = table.Column<bool>("boolean", nullable: false, defaultValue: true),
                GiveawayEndMessage = table.Column<string>("text", nullable: true),
                GiveawayPingRole = table.Column<decimal>("numeric(20,0)", nullable: false),
                StarboardAllowBots = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                StarboardRemoveOnDelete = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                StarboardRemoveOnReactionsClear =
                    table.Column<bool>("boolean", nullable: false, defaultValue: false),
                StarboardRemoveOnBelowThreshold =
                    table.Column<bool>("boolean", nullable: false, defaultValue: true),
                UseStarboardBlacklist = table.Column<bool>("boolean", nullable: false, defaultValue: true),
                StarboardCheckChannels = table.Column<string>("text", nullable: true),
                VotesPassword = table.Column<string>("text", nullable: true),
                VotesChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                VoteEmbed = table.Column<string>("text", nullable: true),
                SuggestionThreadType = table.Column<int>("integer", nullable: false),
                ArchiveOnDeny = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                ArchiveOnAccept = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                ArchiveOnConsider = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                ArchiveOnImplement = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                SuggestButtonMessage = table.Column<string>("text", nullable: true),
                SuggestButtonName = table.Column<string>("text", nullable: true),
                SuggestButtonEmote = table.Column<string>("text", nullable: true),
                ButtonRepostThreshold = table.Column<int>("integer", nullable: false),
                SuggestCommandsType = table.Column<int>("integer", nullable: false),
                AcceptChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                DenyChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                ConsiderChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                ImplementChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                EmoteMode = table.Column<int>("integer", nullable: false),
                SuggestMessage = table.Column<string>("text", nullable: true),
                DenyMessage = table.Column<string>("text", nullable: true),
                AcceptMessage = table.Column<string>("text", nullable: true),
                ImplementMessage = table.Column<string>("text", nullable: true),
                ConsiderMessage = table.Column<string>("text", nullable: true),
                MinSuggestLength = table.Column<int>("integer", nullable: false),
                MaxSuggestLength = table.Column<int>("integer", nullable: false),
                SuggestEmotes = table.Column<string>("text", nullable: true),
                sugnum = table.Column<decimal>("numeric(20,0)", nullable: false),
                sugchan = table.Column<decimal>("numeric(20,0)", nullable: false),
                SuggestButtonChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                Emote1Style = table.Column<int>("integer", nullable: false),
                Emote2Style = table.Column<int>("integer", nullable: false),
                Emote3Style = table.Column<int>("integer", nullable: false),
                Emote4Style = table.Column<int>("integer", nullable: false),
                Emote5Style = table.Column<int>("integer", nullable: false),
                SuggestButtonMessageId = table.Column<decimal>("numeric(20,0)", nullable: false),
                SuggestButtonRepostThreshold = table.Column<int>("integer", nullable: false),
                SuggestButtonColor = table.Column<int>("integer", nullable: false),
                AfkMessage = table.Column<string>("text", nullable: true),
                AutoBotRoleIds = table.Column<string>("text", nullable: true),
                GBEnabled = table.Column<int>("integer", nullable: false),
                GBAction = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                ConfessionLogChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                ConfessionChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                ConfessionBlacklist = table.Column<string>("text", nullable: true),
                MultiGreetType = table.Column<int>("integer", nullable: false),
                MemberRole = table.Column<decimal>("numeric(20,0)", nullable: false),
                TOpenMessage = table.Column<string>("text", nullable: true),
                GStartMessage = table.Column<string>("text", nullable: true),
                GEndMessage = table.Column<string>("text", nullable: true),
                GWinMessage = table.Column<string>("text", nullable: true),
                WarnlogChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                MiniWarnlogChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                SendBoostMessage = table.Column<bool>("boolean", nullable: false),
                GRolesBlacklist = table.Column<string>("text", nullable: true),
                GUsersBlacklist = table.Column<string>("text", nullable: true),
                BoostMessage = table.Column<string>("text", nullable: true),
                BoostMessageChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                BoostMessageDeleteAfter = table.Column<int>("integer", nullable: false),
                GiveawayEmote = table.Column<string>("text", nullable: true),
                TicketChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                TicketCategory = table.Column<decimal>("numeric(20,0)", nullable: false),
                snipeset = table.Column<bool>("boolean", nullable: false),
                AfkLength = table.Column<int>("integer", nullable: false),
                XpTxtTimeout = table.Column<int>("integer", nullable: false),
                XpTxtRate = table.Column<int>("integer", nullable: false),
                XpVoiceRate = table.Column<int>("integer", nullable: false),
                XpVoiceTimeout = table.Column<int>("integer", nullable: false),
                Stars = table.Column<int>("integer", nullable: false),
                AfkType = table.Column<int>("integer", nullable: false),
                AfkDisabledChannels = table.Column<string>("text", nullable: true),
                AfkDel = table.Column<string>("text", nullable: true),
                AfkTimeout = table.Column<int>("integer", nullable: false),
                Joins = table.Column<decimal>("numeric(20,0)", nullable: false),
                Leaves = table.Column<decimal>("numeric(20,0)", nullable: false),
                Star2 = table.Column<string>("text", nullable: true),
                StarboardChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                RepostThreshold = table.Column<int>("integer", nullable: false),
                PreviewLinks = table.Column<int>("integer", nullable: false),
                ReactChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                fwarn = table.Column<int>("integer", nullable: false),
                invwarn = table.Column<int>("integer", nullable: false),
                removeroles = table.Column<int>("integer", nullable: false),
                AutoDeleteGreetMessages = table.Column<bool>("boolean", nullable: false),
                AutoDeleteByeMessages = table.Column<bool>("boolean", nullable: false),
                AutoDeleteGreetMessagesTimer = table.Column<int>("integer", nullable: false),
                AutoDeleteByeMessagesTimer = table.Column<int>("integer", nullable: false),
                GreetMessageChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ByeMessageChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                GreetHook = table.Column<string>("text", nullable: true),
                LeaveHook = table.Column<string>("text", nullable: true),
                SendDmGreetMessage = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                DmGreetMessageText = table.Column<string>("text", nullable: true),
                SendChannelGreetMessage = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                ChannelGreetMessageText = table.Column<string>("text", nullable: true),
                SendChannelByeMessage = table.Column<bool>("boolean", nullable: false, defaultValue: false),
                ChannelByeMessageText = table.Column<string>("text", nullable: true),
                ExclusiveSelfAssignedRoles = table.Column<bool>("boolean", nullable: false),
                AutoDeleteSelfAssignedRoleMessages = table.Column<bool>("boolean", nullable: false),
                LogSettingId = table.Column<int>("integer", nullable: true),
                VerbosePermissions = table.Column<bool>("boolean", nullable: false),
                PermissionRole = table.Column<string>("text", nullable: true),
                FilterInvites = table.Column<bool>("boolean", nullable: false),
                FilterLinks = table.Column<bool>("boolean", nullable: false),
                FilterWords = table.Column<bool>("boolean", nullable: false),
                MuteRoleName = table.Column<string>("text", nullable: true),
                CleverbotChannel = table.Column<decimal>("numeric(20,0)", nullable: false),
                Locale = table.Column<string>("text", nullable: true),
                TimeZoneId = table.Column<string>("text", nullable: true),
                WarningsInitialized = table.Column<bool>("boolean", nullable: false),
                GameVoiceChannel = table.Column<decimal>("numeric(20,0)", nullable: true),
                VerboseErrors = table.Column<bool>("boolean", nullable: false),
                NotifyStreamOffline = table.Column<bool>("boolean", nullable: false),
                WarnExpireHours = table.Column<int>("integer", nullable: false),
                WarnExpireAction = table.Column<int>("integer", nullable: false),
                JoinGraphColor = table.Column<long>("bigint", nullable: false),
                LeaveGraphColor = table.Column<long>("bigint", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GuildConfigs", x => x.Id);
                table.ForeignKey(
                    "FK_GuildConfigs_LogSettings_LogSettingId",
                    x => x.LogSettingId,
                    "LogSettings",
                    "Id");
            });

        migrationBuilder.CreateTable(
            "IgnoredLogChannels",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                LogSettingId = table.Column<int>("integer", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IgnoredLogChannels", x => x.Id);
                table.ForeignKey(
                    "FK_IgnoredLogChannels_LogSettings_LogSettingId",
                    x => x.LogSettingId,
                    "LogSettings",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "PlaylistSong",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                MusicPlaylistId = table.Column<int>("integer", nullable: false),
                Provider = table.Column<string>("text", nullable: true),
                ProviderType = table.Column<int>("integer", nullable: false),
                Title = table.Column<string>("text", nullable: true),
                Uri = table.Column<string>("text", nullable: true),
                Query = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlaylistSong", x => x.Id);
                table.ForeignKey(
                    "FK_PlaylistSong_MusicPlaylists_MusicPlaylistId",
                    x => x.MusicPlaylistId,
                    "MusicPlaylists",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "PollAnswer",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Text = table.Column<string>("text", nullable: true),
                Index = table.Column<int>("integer", nullable: false),
                PollId = table.Column<int>("integer", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PollAnswer", x => x.Id);
                table.ForeignKey(
                    "FK_PollAnswer_Poll_PollId",
                    x => x.PollId,
                    "Poll",
                    "Id");
            });

        migrationBuilder.CreateTable(
            "PollVote",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                VoteIndex = table.Column<int>("integer", nullable: false),
                PollId = table.Column<int>("integer", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PollVote", x => x.Id);
                table.ForeignKey(
                    "FK_PollVote_Poll_PollId",
                    x => x.PollId,
                    "Poll",
                    "Id");
            });

        migrationBuilder.CreateTable(
            "Template",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                OutputSizeX = table.Column<int>("integer", nullable: false),
                OutputSizeY = table.Column<int>("integer", nullable: false),
                TimeOnLevelFormat = table.Column<string>("text", nullable: true),
                TimeOnLevelX = table.Column<int>("integer", nullable: false),
                TimeOnLevelY = table.Column<int>("integer", nullable: false),
                TimeOnLevelFontSize = table.Column<int>("integer", nullable: false),
                TimeOnLevelColor = table.Column<string>("text", nullable: true),
                ShowTimeOnLevel = table.Column<bool>("boolean", nullable: false),
                AwardedX = table.Column<int>("integer", nullable: false),
                AwardedY = table.Column<int>("integer", nullable: false),
                AwardedFontSize = table.Column<int>("integer", nullable: false),
                AwardedColor = table.Column<string>("text", nullable: true),
                ShowAwarded = table.Column<bool>("boolean", nullable: false),
                TemplateUserId = table.Column<int>("integer", nullable: true),
                TemplateGuildId = table.Column<int>("integer", nullable: true),
                TemplateClubId = table.Column<int>("integer", nullable: true),
                TemplateBarId = table.Column<int>("integer", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Template", x => x.Id);
                table.ForeignKey(
                    "FK_Template_TemplateBar_TemplateBarId",
                    x => x.TemplateBarId,
                    "TemplateBar",
                    "Id");
                table.ForeignKey(
                    "FK_Template_TemplateClub_TemplateClubId",
                    x => x.TemplateClubId,
                    "TemplateClub",
                    "Id");
                table.ForeignKey(
                    "FK_Template_TemplateGuild_TemplateGuildId",
                    x => x.TemplateGuildId,
                    "TemplateGuild",
                    "Id");
                table.ForeignKey(
                    "FK_Template_TemplateUser_TemplateUserId",
                    x => x.TemplateUserId,
                    "TemplateUser",
                    "Id");
            });

        migrationBuilder.CreateTable(
            "AntiAltSetting",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                MinAge = table.Column<string>("text", nullable: true),
                Action = table.Column<int>("integer", nullable: false),
                ActionDurationMinutes = table.Column<int>("integer", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AntiAltSetting", x => x.Id);
                table.ForeignKey(
                    "FK_AntiAltSetting_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "AntiRaidSetting",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                UserThreshold = table.Column<int>("integer", nullable: false),
                Seconds = table.Column<int>("integer", nullable: false),
                Action = table.Column<int>("integer", nullable: false),
                PunishDuration = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AntiRaidSetting", x => x.Id);
                table.ForeignKey(
                    "FK_AntiRaidSetting_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "AntiSpamSetting",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                Action = table.Column<int>("integer", nullable: false),
                MessageThreshold = table.Column<int>("integer", nullable: false),
                MuteTime = table.Column<int>("integer", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AntiSpamSetting", x => x.Id);
                table.ForeignKey(
                    "FK_AntiSpamSetting_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "CommandAlias",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Trigger = table.Column<string>("text", nullable: true),
                Mapping = table.Column<string>("text", nullable: true),
                GuildConfigId = table.Column<int>("integer", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CommandAlias", x => x.Id);
                table.ForeignKey(
                    "FK_CommandAlias_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id");
            });

        migrationBuilder.CreateTable(
            "CommandCooldown",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Seconds = table.Column<int>("integer", nullable: false),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                CommandName = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CommandCooldown", x => x.Id);
                table.ForeignKey(
                    "FK_CommandCooldown_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "DelMsgOnCmdChannel",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                State = table.Column<bool>("boolean", nullable: false, defaultValue: true),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DelMsgOnCmdChannel", x => x.Id);
                table.ForeignKey(
                    "FK_DelMsgOnCmdChannel_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "FeedSub",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Url = table.Column<string>("text", nullable: false),
                Message = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FeedSub", x => x.Id);
                table.UniqueConstraint("AK_FeedSub_GuildConfigId_Url", x => new
                {
                    x.GuildConfigId, x.Url
                });
                table.ForeignKey(
                    "FK_FeedSub_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "FilteredWord",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Word = table.Column<string>("text", nullable: true),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilteredWord", x => x.Id);
                table.ForeignKey(
                    "FK_FilteredWord_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "FilterInvitesChannelIds",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilterInvitesChannelIds", x => x.Id);
                table.ForeignKey(
                    "FK_FilterInvitesChannelIds_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "FilterLinksChannelId",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilterLinksChannelId", x => x.Id);
                table.ForeignKey(
                    "FK_FilterLinksChannelId_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "FilterWordsChannelIds",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilterWordsChannelIds", x => x.Id);
                table.ForeignKey(
                    "FK_FilterWordsChannelIds_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "FollowedStream",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Username = table.Column<string>("text", nullable: true),
                Type = table.Column<int>("integer", nullable: false),
                Message = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FollowedStream", x => x.Id);
                table.ForeignKey(
                    "FK_FollowedStream_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "GroupName",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                Number = table.Column<int>("integer", nullable: false),
                Name = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GroupName", x => x.Id);
                table.ForeignKey(
                    "FK_GroupName_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "GuildRepeater",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                LastMessageId = table.Column<decimal>("numeric(20,0)", nullable: true),
                Message = table.Column<string>("text", nullable: true),
                Interval = table.Column<string>("text", nullable: true),
                StartTimeOfDay = table.Column<string>("text", nullable: true),
                NoRedundant = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GuildRepeater", x => x.Id);
                table.ForeignKey(
                    "FK_GuildRepeater_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "MutedUserId",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                roles = table.Column<string>("text", nullable: true),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MutedUserId", x => x.Id);
                table.ForeignKey(
                    "FK_MutedUserId_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "NsfwBlacklitedTag",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                Tag = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NsfwBlacklitedTag", x => x.Id);
                table.ForeignKey(
                    "FK_NsfwBlacklitedTag_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "Permissionv2",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: true),
                PrimaryTarget = table.Column<int>("integer", nullable: false),
                PrimaryTargetId = table.Column<decimal>("numeric(20,0)", nullable: false),
                SecondaryTarget = table.Column<int>("integer", nullable: false),
                SecondaryTargetName = table.Column<string>("text", nullable: true),
                IsCustomCommand = table.Column<bool>("boolean", nullable: false),
                State = table.Column<bool>("boolean", nullable: false),
                Index = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Permissionv2", x => x.Id);
                table.ForeignKey(
                    "FK_Permissionv2_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id");
            });

        migrationBuilder.CreateTable(
            "ReactionRoleMessage",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                MessageId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Exclusive = table.Column<bool>("boolean", nullable: false),
                Index = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReactionRoleMessage", x => x.Id);
                table.ForeignKey(
                    "FK_ReactionRoleMessage_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "StreamRoleSettings",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                Enabled = table.Column<bool>("boolean", nullable: false),
                AddRoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                FromRoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Keyword = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StreamRoleSettings", x => x.Id);
                table.ForeignKey(
                    "FK_StreamRoleSettings_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "UnbanTimer",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UnbanAt = table.Column<DateTime>("timestamp without time zone", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UnbanTimer", x => x.Id);
                table.ForeignKey(
                    "FK_UnbanTimer_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "UnmuteTimer",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: true),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UnmuteAt = table.Column<DateTime>("timestamp without time zone", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UnmuteTimer", x => x.Id);
                table.ForeignKey(
                    "FK_UnmuteTimer_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id");
            });

        migrationBuilder.CreateTable(
            "UnroleTimer",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UnbanAt = table.Column<DateTime>("timestamp without time zone", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UnroleTimer", x => x.Id);
                table.ForeignKey(
                    "FK_UnroleTimer_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "VcRoleInfo",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                VoiceChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VcRoleInfo", x => x.Id);
                table.ForeignKey(
                    "FK_VcRoleInfo_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "WarningPunishment",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                Count = table.Column<int>("integer", nullable: false),
                Punishment = table.Column<int>("integer", nullable: false),
                Time = table.Column<int>("integer", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WarningPunishment", x => x.Id);
                table.ForeignKey(
                    "FK_WarningPunishment_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "WarningPunishment2",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                Count = table.Column<int>("integer", nullable: false),
                Punishment = table.Column<int>("integer", nullable: false),
                Time = table.Column<int>("integer", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WarningPunishment2", x => x.Id);
                table.ForeignKey(
                    "FK_WarningPunishment2_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "XpSettings",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                XpRoleRewardExclusive = table.Column<bool>("boolean", nullable: false),
                NotifyMessage = table.Column<string>("text", nullable: true),
                ServerExcluded = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_XpSettings", x => x.Id);
                table.ForeignKey(
                    "FK_XpSettings_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "AntiSpamIgnore",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                AntiSpamSettingId = table.Column<int>("integer", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AntiSpamIgnore", x => x.Id);
                table.ForeignKey(
                    "FK_AntiSpamIgnore_AntiSpamSetting_AntiSpamSettingId",
                    x => x.AntiSpamSettingId,
                    "AntiSpamSetting",
                    "Id");
            });

        migrationBuilder.CreateTable(
            "ReactionRole",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                EmoteName = table.Column<string>("text", nullable: true),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ReactionRoleMessageId = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReactionRole", x => x.Id);
                table.ForeignKey(
                    "FK_ReactionRole_ReactionRoleMessage_ReactionRoleMessageId",
                    x => x.ReactionRoleMessageId,
                    "ReactionRoleMessage",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "StreamRoleBlacklistedUser",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Username = table.Column<string>("text", nullable: true),
                StreamRoleSettingsId = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StreamRoleBlacklistedUser", x => x.Id);
                table.ForeignKey(
                    "FK_StreamRoleBlacklistedUser_StreamRoleSettings_StreamRoleSett~",
                    x => x.StreamRoleSettingsId,
                    "StreamRoleSettings",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "StreamRoleWhitelistedUser",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                StreamRoleSettingsId = table.Column<int>("integer", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Username = table.Column<string>("text", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StreamRoleWhitelistedUser", x => x.Id);
                table.ForeignKey(
                    "FK_StreamRoleWhitelistedUser_StreamRoleSettings_StreamRoleSett~",
                    x => x.StreamRoleSettingsId,
                    "StreamRoleSettings",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "ExcludedItem",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ItemId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ItemType = table.Column<int>("integer", nullable: false),
                XpSettingsId = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExcludedItem", x => x.Id);
                table.ForeignKey(
                    "FK_ExcludedItem_XpSettings_XpSettingsId",
                    x => x.XpSettingsId,
                    "XpSettings",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "XpCurrencyReward",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                XpSettingsId = table.Column<int>("integer", nullable: false),
                Level = table.Column<int>("integer", nullable: false),
                Amount = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_XpCurrencyReward", x => x.Id);
                table.ForeignKey(
                    "FK_XpCurrencyReward_XpSettings_XpSettingsId",
                    x => x.XpSettingsId,
                    "XpSettings",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "XpRoleReward",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                XpSettingsId = table.Column<int>("integer", nullable: false),
                Level = table.Column<int>("integer", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_XpRoleReward", x => x.Id);
                table.ForeignKey(
                    "FK_XpRoleReward_XpSettings_XpSettingsId",
                    x => x.XpSettingsId,
                    "XpSettings",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_AntiAltSetting_GuildConfigId",
            "AntiAltSetting",
            "GuildConfigId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_AntiRaidSetting_GuildConfigId",
            "AntiRaidSetting",
            "GuildConfigId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_AntiSpamIgnore_AntiSpamSettingId",
            "AntiSpamIgnore",
            "AntiSpamSettingId");

        migrationBuilder.CreateIndex(
            "IX_AntiSpamSetting_GuildConfigId",
            "AntiSpamSetting",
            "GuildConfigId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_BanTemplates_GuildId",
            "BanTemplates",
            "GuildId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_CommandAlias_GuildConfigId",
            "CommandAlias",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_CommandCooldown_GuildConfigId",
            "CommandCooldown",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_DelMsgOnCmdChannel_GuildConfigId",
            "DelMsgOnCmdChannel",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_DiscordPermOverrides_GuildId_Command",
            "DiscordPermOverrides",
            [
                "GuildId", "Command"
            ],
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_DiscordUser_TotalXp",
            "DiscordUser",
            "TotalXp");

        migrationBuilder.CreateIndex(
            "IX_DiscordUser_UserId",
            "DiscordUser",
            "UserId");

        migrationBuilder.CreateIndex(
            "IX_ExcludedItem_XpSettingsId",
            "ExcludedItem",
            "XpSettingsId");

        migrationBuilder.CreateIndex(
            "IX_FilteredWord_GuildConfigId",
            "FilteredWord",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_FilterInvitesChannelIds_GuildConfigId",
            "FilterInvitesChannelIds",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_FilterLinksChannelId_GuildConfigId",
            "FilterLinksChannelId",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_FilterWordsChannelIds_GuildConfigId",
            "FilterWordsChannelIds",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_FollowedStream_GuildConfigId",
            "FollowedStream",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_GroupName_GuildConfigId_Number",
            "GroupName",
            [
                "GuildConfigId", "Number"
            ],
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_GuildConfigs_GuildId",
            "GuildConfigs",
            "GuildId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_GuildConfigs_LogSettingId",
            "GuildConfigs",
            "LogSettingId");

        migrationBuilder.CreateIndex(
            "IX_GuildConfigs_WarnExpireHours",
            "GuildConfigs",
            "WarnExpireHours");

        migrationBuilder.CreateIndex(
            "IX_GuildRepeater_GuildConfigId",
            "GuildRepeater",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_IgnoredLogChannels_LogSettingId",
            "IgnoredLogChannels",
            "LogSettingId");

        migrationBuilder.CreateIndex(
            "IX_MusicPlayerSettings_GuildId",
            "MusicPlayerSettings",
            "GuildId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_MutedUserId_GuildConfigId",
            "MutedUserId",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_NsfwBlacklitedTag_GuildConfigId",
            "NsfwBlacklitedTag",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_Permission_NextId",
            "Permission",
            "NextId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_Permissionv2_GuildConfigId",
            "Permissionv2",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_PlaylistSong_MusicPlaylistId",
            "PlaylistSong",
            "MusicPlaylistId");

        migrationBuilder.CreateIndex(
            "IX_Poll_GuildId",
            "Poll",
            "GuildId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_PollAnswer_PollId",
            "PollAnswer",
            "PollId");

        migrationBuilder.CreateIndex(
            "IX_PollVote_PollId",
            "PollVote",
            "PollId");

        migrationBuilder.CreateIndex(
            "IX_Quotes_GuildId",
            "Quotes",
            "GuildId");

        migrationBuilder.CreateIndex(
            "IX_Quotes_Keyword",
            "Quotes",
            "Keyword");

        migrationBuilder.CreateIndex(
            "IX_ReactionRole_ReactionRoleMessageId",
            "ReactionRole",
            "ReactionRoleMessageId");

        migrationBuilder.CreateIndex(
            "IX_ReactionRoleMessage_GuildConfigId",
            "ReactionRoleMessage",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_Reminders_When",
            "Reminders",
            "When");

        migrationBuilder.CreateIndex(
            "IX_SelfAssignableRoles_GuildId_RoleId",
            "SelfAssignableRoles",
            [
                "GuildId", "RoleId"
            ],
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_StreamRoleBlacklistedUser_StreamRoleSettingsId",
            "StreamRoleBlacklistedUser",
            "StreamRoleSettingsId");

        migrationBuilder.CreateIndex(
            "IX_StreamRoleSettings_GuildConfigId",
            "StreamRoleSettings",
            "GuildConfigId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_StreamRoleWhitelistedUser_StreamRoleSettingsId",
            "StreamRoleWhitelistedUser",
            "StreamRoleSettingsId");

        migrationBuilder.CreateIndex(
            "IX_Template_TemplateBarId",
            "Template",
            "TemplateBarId");

        migrationBuilder.CreateIndex(
            "IX_Template_TemplateClubId",
            "Template",
            "TemplateClubId");

        migrationBuilder.CreateIndex(
            "IX_Template_TemplateGuildId",
            "Template",
            "TemplateGuildId");

        migrationBuilder.CreateIndex(
            "IX_Template_TemplateUserId",
            "Template",
            "TemplateUserId");

        migrationBuilder.CreateIndex(
            "IX_UnbanTimer_GuildConfigId",
            "UnbanTimer",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_UnmuteTimer_GuildConfigId",
            "UnmuteTimer",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_UnroleTimer_GuildConfigId",
            "UnroleTimer",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_UserXpStats_AwardedXp",
            "UserXpStats",
            "AwardedXp");

        migrationBuilder.CreateIndex(
            "IX_UserXpStats_GuildId",
            "UserXpStats",
            "GuildId");

        migrationBuilder.CreateIndex(
            "IX_UserXpStats_UserId",
            "UserXpStats",
            "UserId");

        migrationBuilder.CreateIndex(
            "IX_UserXpStats_UserId_GuildId",
            "UserXpStats",
            [
                "UserId", "GuildId"
            ],
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_UserXpStats_Xp",
            "UserXpStats",
            "Xp");

        migrationBuilder.CreateIndex(
            "IX_VcRoleInfo_GuildConfigId",
            "VcRoleInfo",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_WarningPunishment_GuildConfigId",
            "WarningPunishment",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_WarningPunishment2_GuildConfigId",
            "WarningPunishment2",
            "GuildConfigId");

        migrationBuilder.CreateIndex(
            "IX_Warnings_DateAdded",
            "Warnings",
            "DateAdded");

        migrationBuilder.CreateIndex(
            "IX_Warnings_GuildId",
            "Warnings",
            "GuildId");

        migrationBuilder.CreateIndex(
            "IX_Warnings_UserId",
            "Warnings",
            "UserId");

        migrationBuilder.CreateIndex(
            "IX_XpCurrencyReward_XpSettingsId",
            "XpCurrencyReward",
            "XpSettingsId");

        migrationBuilder.CreateIndex(
            "IX_XpRoleReward_XpSettingsId_Level",
            "XpRoleReward",
            [
                "XpSettingsId", "Level"
            ],
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_XpSettings_GuildConfigId",
            "XpSettings",
            "GuildConfigId",
            unique: true);
    }

    /// <inheritdoc />
    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "AFK");

        migrationBuilder.DropTable(
            "AntiAltSetting");

        migrationBuilder.DropTable(
            "AntiRaidSetting");

        migrationBuilder.DropTable(
            "AntiSpamIgnore");

        migrationBuilder.DropTable(
            "AuthCodes");

        migrationBuilder.DropTable(
            "AutoBanRoles");

        migrationBuilder.DropTable(
            "AutoBanWords");

        migrationBuilder.DropTable(
            "AutoCommands");

        migrationBuilder.DropTable(
            "AutoPublish");

        migrationBuilder.DropTable(
            "BanTemplates");

        migrationBuilder.DropTable(
            "Blacklist");

        migrationBuilder.DropTable(
            "ChatTriggers");

        migrationBuilder.DropTable(
            "CommandAlias");

        migrationBuilder.DropTable(
            "CommandCooldown");

        migrationBuilder.DropTable(
            "CommandStats");

        migrationBuilder.DropTable(
            "Confessions");

        migrationBuilder.DropTable(
            "DelMsgOnCmdChannel");

        migrationBuilder.DropTable(
            "DiscordPermOverrides");

        migrationBuilder.DropTable(
            "DiscordUser");

        migrationBuilder.DropTable(
            "ExcludedItem");

        migrationBuilder.DropTable(
            "FeedSub");

        migrationBuilder.DropTable(
            "FilteredWord");

        migrationBuilder.DropTable(
            "FilterInvitesChannelIds");

        migrationBuilder.DropTable(
            "FilterLinksChannelId");

        migrationBuilder.DropTable(
            "FilterWordsChannelIds");

        migrationBuilder.DropTable(
            "FollowedStream");

        migrationBuilder.DropTable(
            "Giveaways");

        migrationBuilder.DropTable(
            "GlobalUserBalance");

        migrationBuilder.DropTable(
            "GroupName");

        migrationBuilder.DropTable(
            "GuildRepeater");

        migrationBuilder.DropTable(
            "GuildUserBalance");

        migrationBuilder.DropTable(
            "Highlights");

        migrationBuilder.DropTable(
            "HighlightSettings");

        migrationBuilder.DropTable(
            "IgnoredLogChannels");

        migrationBuilder.DropTable(
            "JoinLeaveLogs");

        migrationBuilder.DropTable(
            "MultiGreets");

        migrationBuilder.DropTable(
            "MusicPlayerSettings");

        migrationBuilder.DropTable(
            "MutedUserId");

        migrationBuilder.DropTable(
            "NsfwBlacklitedTag");

        migrationBuilder.DropTable(
            "OwnerOnly");

        migrationBuilder.DropTable(
            "Permission");

        migrationBuilder.DropTable(
            "Permissionv2");

        migrationBuilder.DropTable(
            "PlaylistSong");

        migrationBuilder.DropTable(
            "PollAnswer");

        migrationBuilder.DropTable(
            "PollVote");

        migrationBuilder.DropTable(
            "PublishUserBlacklist");

        migrationBuilder.DropTable(
            "PublishWordBlacklist");

        migrationBuilder.DropTable(
            "Quotes");

        migrationBuilder.DropTable(
            "ReactionRole");

        migrationBuilder.DropTable(
            "Reminders");

        migrationBuilder.DropTable(
            "RoleGreets");

        migrationBuilder.DropTable(
            "RoleStateSettings");

        migrationBuilder.DropTable(
            "RotatingStatus");

        migrationBuilder.DropTable(
            "SelfAssignableRoles");

        migrationBuilder.DropTable(
            "ServerRecoveryStore");

        migrationBuilder.DropTable(
            "Starboard");

        migrationBuilder.DropTable(
            "StatusRoles");

        migrationBuilder.DropTable(
            "StreamRoleBlacklistedUser");

        migrationBuilder.DropTable(
            "StreamRoleWhitelistedUser");

        migrationBuilder.DropTable(
            "Suggestions");

        migrationBuilder.DropTable(
            "SuggestThreads");

        migrationBuilder.DropTable(
            "SuggestVotes");

        migrationBuilder.DropTable(
            "Template");

        migrationBuilder.DropTable(
            "TransactionHistory");

        migrationBuilder.DropTable(
            "UnbanTimer");

        migrationBuilder.DropTable(
            "UnmuteTimer");

        migrationBuilder.DropTable(
            "UnroleTimer");

        migrationBuilder.DropTable(
            "UserRoleStates");

        migrationBuilder.DropTable(
            "UserXpStats");

        migrationBuilder.DropTable(
            "VcRoleInfo");

        migrationBuilder.DropTable(
            "VoteRoles");

        migrationBuilder.DropTable(
            "Votes");

        migrationBuilder.DropTable(
            "WarningPunishment");

        migrationBuilder.DropTable(
            "WarningPunishment2");

        migrationBuilder.DropTable(
            "Warnings");

        migrationBuilder.DropTable(
            "Warnings2");

        migrationBuilder.DropTable(
            "XpCurrencyReward");

        migrationBuilder.DropTable(
            "XpRoleReward");

        migrationBuilder.DropTable(
            "AntiSpamSetting");

        migrationBuilder.DropTable(
            "MusicPlaylists");

        migrationBuilder.DropTable(
            "Poll");

        migrationBuilder.DropTable(
            "ReactionRoleMessage");

        migrationBuilder.DropTable(
            "StreamRoleSettings");

        migrationBuilder.DropTable(
            "TemplateBar");

        migrationBuilder.DropTable(
            "TemplateClub");

        migrationBuilder.DropTable(
            "TemplateGuild");

        migrationBuilder.DropTable(
            "TemplateUser");

        migrationBuilder.DropTable(
            "XpSettings");

        migrationBuilder.DropTable(
            "GuildConfigs");

        migrationBuilder.DropTable(
            "LogSettings");
    }
}