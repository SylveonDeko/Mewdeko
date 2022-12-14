using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddCommandLogChannels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<ulong>("CommandLogChannel", "GuildConfigs", defaultValue: 0);
}