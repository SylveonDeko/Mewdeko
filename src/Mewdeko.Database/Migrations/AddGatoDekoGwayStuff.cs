using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddGatoDekoGwayStuff : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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
    }
}