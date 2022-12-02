using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class StarboardUpgrade : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)

    {
        migrationBuilder.AddColumn<bool>("UseStarboardBlacklist", "GuildConfigs", defaultValue: true);
        migrationBuilder.AddColumn<string>("StarboardCheckChannels", "GuildConfigs", defaultValue: "0");
    }
}