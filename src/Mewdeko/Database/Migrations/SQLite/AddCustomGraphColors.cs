using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class AddCustomGraphColors : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<uint>("JoinGraphColor", "GuildConfigs", defaultValue: 4294956800);
        migrationBuilder.AddColumn<uint>("LeaveGraphColor", "GuildConfigs", defaultValue: 4294956800);
    }
}