using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class RemoveReputation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Reputation");
        migrationBuilder.DropTable("RepBlacklist");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Reputation");
        migrationBuilder.DropTable("RepBlacklist");
    }
}