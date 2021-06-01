using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class goodbyebotconfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoCommands",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    CommandText = table.Column<string>(nullable: true),
                    ChannelId = table.Column<ulong>(nullable: false),
                    ChannelName = table.Column<string>(nullable: true),
                    GuildId = table.Column<ulong>(nullable: true),
                    GuildName = table.Column<string>(nullable: true),
                    VoiceChannelId = table.Column<ulong>(nullable: true),
                    VoiceChannelName = table.Column<string>(nullable: true),
                    Interval = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Blacklist",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    ItemId = table.Column<ulong>(nullable: false),
                    Type = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blacklist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RotatingStatus",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Status = table.Column<string>(nullable: true),
                    Type = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RotatingStatus", x => x.Id);
                });
            
            Helper.BotconfigClear(migrationBuilder);
            
            migrationBuilder.DropTable(
                name: "BlacklistItem");
            
            migrationBuilder.DropTable(
                name: "RaceAnimals");
            
            migrationBuilder.DropTable(
                name: "EightballResponses");

            migrationBuilder.DropTable(
                name: "PlayingStatus");

            migrationBuilder.DropTable(
                name: "StartupCommand");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoCommands");

            migrationBuilder.DropTable(
                name: "Blacklist");

            migrationBuilder.DropTable(
                name: "RotatingStatus");

            migrationBuilder.CreateTable(
                name: "BotConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BetflipMultiplier = table.Column<float>(type: "REAL", nullable: false),
                    Betroll100Multiplier = table.Column<float>(type: "REAL", nullable: false),
                    Betroll67Multiplier = table.Column<float>(type: "REAL", nullable: false),
                    Betroll91Multiplier = table.Column<float>(type: "REAL", nullable: false),
                    BufferSize = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CheckForUpdates = table.Column<int>(type: "INTEGER", nullable: false),
                    ConsoleOutputType = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrencyDropAmount = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrencyDropAmountMax = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrencyGenerationChance = table.Column<float>(type: "REAL", nullable: false),
                    CurrencyGenerationCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrencyGenerationPassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    CurrencyName = table.Column<string>(type: "TEXT", nullable: true),
                    CurrencyPluralName = table.Column<string>(type: "TEXT", nullable: true),
                    CurrencySign = table.Column<string>(type: "TEXT", nullable: true),
                    CustomReactionsStartWith = table.Column<bool>(type: "INTEGER", nullable: false),
                    DMHelpString = table.Column<string>(type: "TEXT", nullable: true),
                    DailyCurrencyDecay = table.Column<float>(type: "REAL", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DefaultPrefix = table.Column<string>(type: "TEXT", nullable: true),
                    DivorcePriceMultiplier = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorColor = table.Column<string>(type: "TEXT", nullable: true),
                    ForwardMessages = table.Column<bool>(type: "INTEGER", nullable: false),
                    ForwardToAllOwners = table.Column<bool>(type: "INTEGER", nullable: false),
                    GroupGreets = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasMigratedBotSettings = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    HasMigratedGamblingSettings = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    HasMigratedXpSettings = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    HelpString = table.Column<string>(type: "TEXT", nullable: true),
                    LastCurrencyDecay = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Locale = table.Column<string>(type: "TEXT", nullable: true),
                    MaxBet = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxXpMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    MigrationVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    MinBet = table.Column<int>(type: "INTEGER", nullable: false),
                    MinWaifuPrice = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumBetAmount = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumTriviaWinReq = table.Column<int>(type: "INTEGER", nullable: false),
                    OkColor = table.Column<string>(type: "TEXT", nullable: true),
                    PatreonCurrencyPerCent = table.Column<float>(type: "REAL", nullable: false),
                    PermissionVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    RemindMessageFormat = table.Column<string>(type: "TEXT", nullable: true),
                    RotatingStatuses = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimelyCurrency = table.Column<int>(type: "INTEGER", nullable: false),
                    TimelyCurrencyPeriod = table.Column<int>(type: "INTEGER", nullable: false),
                    TriviaCurrencyReward = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdateString = table.Column<string>(type: "TEXT", nullable: true),
                    VoiceXpPerMinute = table.Column<double>(type: "REAL", nullable: false),
                    WaifuGiftMultiplier = table.Column<int>(type: "INTEGER", nullable: false),
                    XpMinutesTimeout = table.Column<int>(type: "INTEGER", nullable: false),
                    XpPerMessage = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlacklistItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(type: "INTEGER", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ItemId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlacklistItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlacklistItem_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayingStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(type: "INTEGER", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayingStatus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayingStatus_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StartupCommand",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ChannelName = table.Column<string>(type: "TEXT", nullable: true),
                    CommandText = table.Column<string>(type: "TEXT", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    GuildName = table.Column<string>(type: "TEXT", nullable: true),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Interval = table.Column<int>(type: "INTEGER", nullable: false),
                    VoiceChannelId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    VoiceChannelName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StartupCommand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StartupCommand_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }
    }
}
