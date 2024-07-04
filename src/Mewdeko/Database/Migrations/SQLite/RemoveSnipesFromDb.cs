using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class RemoveSnipesFromDb : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable("SnipeStore");
}