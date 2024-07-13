using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class MoreStarboardUpgrades : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("StarboardAllowBots", "GuildConfigs", defaultValue: true);
        migrationBuilder.AddColumn<bool>("StarboardRemoveOnDelete", "GuildConfigs", defaultValue: false);
        migrationBuilder.AddColumn<bool>("StarboardRemoveOnReactionsClear", "GuildConfigs", defaultValue: false);
        migrationBuilder.AddColumn<bool>("StarboardRemoveOnBelowThreshold", "GuildConfigs", defaultValue: true);
    }
}