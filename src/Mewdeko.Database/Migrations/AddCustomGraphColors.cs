using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddCustomGraphColors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<uint>("JoinGraphColor", "GuildConfigs", defaultValue: 16766720);
        migrationBuilder.AddColumn<uint>("LeaveGraphColor", "GuildConfigs", defaultValue: 16766720);
    }
}
