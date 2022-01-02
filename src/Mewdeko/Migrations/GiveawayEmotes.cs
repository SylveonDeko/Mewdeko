using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations;

public partial class giveawayemotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            "GiveawayEmote",
            "GuildConfigs",
            "TEXT",
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            "GiveawayEmote",
            "GuildConfigs");
}