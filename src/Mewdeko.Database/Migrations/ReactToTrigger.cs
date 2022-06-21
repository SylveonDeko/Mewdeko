using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class ReactToTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            "ReactToTrigger",
            "CustomReactions",
            "Integer",
            defaultValue: 0,
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            "CustomReactions",
            "ReactToTrigger");
}