using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class gamblingsettingsmigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "WaifuItem",
                nullable: true);

            // if this migration is running, it means the user had the database
            // prior to this patch, therefore migraton to .yml is required
            // so the default value is manually changed from true to false
            // but if the user had the database, the snapshot default value
            // (true) will be used
            migrationBuilder.AddColumn<bool>(
                name: "HasMigratedGamblingSettings",
                table: "BotConfig",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "WaifuItem");

            migrationBuilder.DropColumn(
                name: "HasMigratedGamblingSettings",
                table: "BotConfig");
        }
    }
}
