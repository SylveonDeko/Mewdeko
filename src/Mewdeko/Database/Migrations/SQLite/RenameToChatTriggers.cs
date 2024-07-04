using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class RenameToChatTriggers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.RenameTable("CustomReactions", newName: "ChatTriggers");
}