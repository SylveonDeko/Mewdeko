using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class boostmessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoostMessage",
                table: "GuildConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "BoostMessageChannelId",
                table: "GuildConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<int>(
                name: "BoostMessageDeleteAfter",
                table: "GuildConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SendBoostMessage",
                table: "GuildConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoostMessage",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "BoostMessageChannelId",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "BoostMessageDeleteAfter",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "SendBoostMessage",
                table: "GuildConfigs");
        }
    }
}