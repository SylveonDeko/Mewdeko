using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class AddCommandLogChannels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<ulong>("CommandLogChannel", "GuildConfigs", defaultValue: 0);
}