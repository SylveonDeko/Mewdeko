using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class botsettingsmigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasMigratedBotSettings",
                table: "BotConfig",
                nullable: false,
                defaultValue: true);
            
            // if this migration is running, it means the user had the database
            // prior to this patch, therefore migraton to .yml is required
            migrationBuilder.Sql("UPDATE BotConfig SET HasMigratedBotSettings = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasMigratedBotSettings",
                table: "BotConfig");
        }
    }
}
