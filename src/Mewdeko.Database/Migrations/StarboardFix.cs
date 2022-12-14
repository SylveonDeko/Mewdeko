using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class StarboardFix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Star2", "GuildConfigs", defaultValue: "⭐");
        migrationBuilder.AddColumn<int>("RepostThreshold", "GuildConfigs", defaultValue: 5);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn("Star", "GuildConfigs");
}