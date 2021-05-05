using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class guildtimezoneandlocale : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "GuildConfigs",
                nullable: true,
                defaultValue: null);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "GuildConfigs",
                nullable: true,
                defaultValue: null);

            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "BotConfig",
                nullable: true,
                defaultValue: null);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Locale",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "Locale",
                table: "BotConfig");
        }
    }
}
