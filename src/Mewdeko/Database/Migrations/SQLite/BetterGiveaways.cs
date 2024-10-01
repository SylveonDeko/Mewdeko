using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class BetterGiveaways : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "GiveawayEndMessage",
            "GuildConfigs",
            "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<ulong>(
            "GiveawayPingRole",
            "GuildConfigs",
            "INTEGER",
            defaultValue: 0,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            "GiveawayBanner",
            "GuildConfigs",
            "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            "GiveawayEmbedColor",
            "GuildConfigs",
            "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            "GiveawayWinEmbedColor",
            "GuildConfigs",
            "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            "DmOnGiveawayWin",
            "GuildConfigs",
            "INTEGER",
            defaultValue: false,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            "Banner",
            "Giveaways",
            "TEXT",
            nullable: true);
    }
}