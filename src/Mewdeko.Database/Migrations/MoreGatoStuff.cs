using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class MoreGatoStuff : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GiveawayEndMessage",
            table: "GuildConfigs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<ulong>(
            name: "GiveawayPingRole",
            table: "GuildConfigs",
            type: "INTEGER",
            defaultValue: 0,
            nullable: true);
    }
}