using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class AddXpBanner : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("XpImgUrl", "GuildConfigs", "text", nullable: true);
    }
}