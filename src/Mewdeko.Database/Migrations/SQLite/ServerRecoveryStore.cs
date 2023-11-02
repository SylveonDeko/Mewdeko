using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class AddServerRecoveryStore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ServerRecoveryStore",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                RecoveryKey = table.Column<string>(type: "TEXT", nullable: false),
                TwoFactorKey = table.Column<string>(type: "TEXT", nullable: false),
                DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServerRecoveryStore", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ServerRecoveryStore");
    }
}