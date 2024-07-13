using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class StarboardUpgrade : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)

    {
        migrationBuilder.AddColumn<bool>("UseStarboardBlacklist", "GuildConfigs", defaultValue: true);
        migrationBuilder.AddColumn<string>("StarboardCheckChannels", "GuildConfigs", defaultValue: "0");
    }
}