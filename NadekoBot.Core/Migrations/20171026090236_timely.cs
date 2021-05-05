using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class timely : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TimelyCurrency",
                table: "BotConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TimelyCurrencyPeriod",
                table: "BotConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimelyCurrency",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "TimelyCurrencyPeriod",
                table: "BotConfig");
        }
    }
}
