using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class warnexpiry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarnExpireAction",
                table: "GuildConfigs",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WarnExpireHours",
                table: "GuildConfigs",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_GuildConfigs_WarnExpireHours",
                table: "GuildConfigs",
                column: "WarnExpireHours");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GuildConfigs_WarnExpireHours",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "WarnExpireAction",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "WarnExpireHours",
                table: "GuildConfigs");
        }
    }
}
