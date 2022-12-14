using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class HereThereBeDragons : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<bool>("IsDragon", "DiscordUser", defaultValue: false, nullable: false);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("IsDragon", "DiscordUser");
}