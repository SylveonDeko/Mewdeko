using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class Boostmessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "BoostMessage",
            "GuildConfigs",
            "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<ulong>(
            "BoostMessageChannelId",
            "GuildConfigs",
            "INTEGER",
            nullable: false,
            defaultValue: 0ul);

        migrationBuilder.AddColumn<int>(
            "BoostMessageDeleteAfter",
            "GuildConfigs",
            "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            "SendBoostMessage",
            "GuildConfigs",
            "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "BoostMessage",
            "GuildConfigs");

        migrationBuilder.DropColumn(
            "BoostMessageChannelId",
            "GuildConfigs");

        migrationBuilder.DropColumn(
            "BoostMessageDeleteAfter",
            "GuildConfigs");

        migrationBuilder.DropColumn(
            "SendBoostMessage",
            "GuildConfigs");
    }
}