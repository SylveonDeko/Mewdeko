using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class okerrorcolors : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorColor",
                table: "BotConfig",
                nullable: false,
                defaultValue: "ee281f");

            migrationBuilder.AddColumn<string>(
                name: "OkColor",
                table: "BotConfig",
                nullable: false,
                defaultValue: "71cd40");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorColor",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "OkColor",
                table: "BotConfig");
        }
    }
}
