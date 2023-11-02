using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.PostgreSQL;

public partial class BetterGiveaways : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GiveawayEndMessage",
            table: "GuildConfigs",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "GiveawayPingRole",
            table: "GuildConfigs",
            type: "bigint",
            defaultValue: 0L,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GiveawayBanner",
            table: "GuildConfigs",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GiveawayEmbedColor",
            table: "GuildConfigs",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GiveawayWinEmbedColor",
            table: "GuildConfigs",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "DmOnGiveawayWin",
            table: "GuildConfigs",
            type: "bigint",
            defaultValue: 0,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Banner",
            table: "Giveaways",
            type: "text",
            nullable: true);
    }
}