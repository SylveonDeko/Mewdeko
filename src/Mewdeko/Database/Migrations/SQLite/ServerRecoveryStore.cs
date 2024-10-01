using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class AddServerRecoveryStore : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "ServerRecoveryStore",
            table => new
            {
                Id = table.Column<int>("INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                GuildId = table.Column<ulong>("INTEGER", nullable: false),
                RecoveryKey = table.Column<string>("TEXT", nullable: false),
                TwoFactorKey = table.Column<string>("TEXT", nullable: false),
                DateAdded = table.Column<DateTime>("TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServerRecoveryStore", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "ServerRecoveryStore");
    }
}