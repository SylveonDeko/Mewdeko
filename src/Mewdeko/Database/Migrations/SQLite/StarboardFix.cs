using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class StarboardFix : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Star2", "GuildConfigs", defaultValue: "⭐");
        migrationBuilder.AddColumn<int>("RepostThreshold", "GuildConfigs", defaultValue: 5);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn("Star", "GuildConfigs");
}