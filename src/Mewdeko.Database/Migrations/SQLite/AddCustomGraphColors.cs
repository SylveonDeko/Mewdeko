using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class AddCustomGraphColors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<uint>("JoinGraphColor", "GuildConfigs", defaultValue: 4294956800);
        migrationBuilder.AddColumn<uint>("LeaveGraphColor", "GuildConfigs", defaultValue: 4294956800);
    }
}