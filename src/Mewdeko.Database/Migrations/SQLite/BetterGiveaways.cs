using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class BetterGiveaways : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GiveawayEndMessage",
            table: "GuildConfigs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<ulong>(
            name: "GiveawayPingRole",
            table: "GuildConfigs",
            type: "INTEGER",
            defaultValue: 0,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "GiveawayBanner",
            table: "GuildConfigs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GiveawayEmbedColor",
            table: "GuildConfigs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GiveawayWinEmbedColor",
            table: "GuildConfigs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "DmOnGiveawayWin",
            table: "GuildConfigs",
            type: "INTEGER",
            defaultValue: false,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Banner",
            table: "Giveaways",
            type: "TEXT",
            nullable: true);
    }
}