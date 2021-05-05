using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class voicexp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxXpMinutes",
                table: "BotConfig",
                nullable: false,
                defaultValue: 720);

            migrationBuilder.AddColumn<double>(
                name: "VoiceXpPerMinute",
                table: "BotConfig",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxXpMinutes",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "VoiceXpPerMinute",
                table: "BotConfig");
        }
    }
}
