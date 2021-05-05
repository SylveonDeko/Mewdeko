using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class dateadded : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "WaifuUpdates",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "WaifuInfo",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "PokeGame",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "SelfAssignableRoles",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "Reminders",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "RaceAnimals",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "Quotes",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "PlaylistSong",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "PlayingStatus",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "Permission",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "MutedUserId",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "MusicPlaylists",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "ModulePrefixes",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "LogSettings",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "IgnoredVoicePresenceCHannels",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "IgnoredLogChannels",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "GuildRepeater",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "GuildConfigs",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "GCChannelId",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "FollowedStream",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "FilteredWord",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "FilterChannelId",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "EightBallResponses",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "Donators",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "DiscordUser",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "CustomReactions",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "CurrencyTransactions",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "Currency",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "ConversionUnits",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "CommandPrice",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "CommandCooldown",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "ClashOfClans",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "ClashCallers",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "BotConfig",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "BlacklistItem",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "AntiSpamSetting",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "AntiSpamIgnore",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "AntiRaidSetting",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "WaifuUpdates");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "WaifuInfo");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "PokeGame");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "SelfAssignableRoles");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "RaceAnimals");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "PlaylistSong");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "PlayingStatus");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "Permission");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "MutedUserId");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "MusicPlaylists");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "ModulePrefixes");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "LogSettings");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "IgnoredVoicePresenceCHannels");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "IgnoredLogChannels");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "GuildRepeater");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "GCChannelId");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "FollowedStream");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "FilteredWord");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "FilterChannelId");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "EightBallResponses");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "Donators");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "DiscordUser");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "CustomReactions");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "CurrencyTransactions");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "Currency");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "ConversionUnits");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "CommandPrice");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "CommandCooldown");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "ClashOfClans");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "ClashCallers");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "BlacklistItem");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "AntiSpamSetting");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "AntiSpamIgnore");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "AntiRaidSetting");
        }
    }
}
