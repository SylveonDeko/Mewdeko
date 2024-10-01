using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class CreateJoinLeaveLogsSystemThingie : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "JoinLeaveLogs",
            table => new
            {
                Id = table.Column<int>("INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                GuildId = table.Column<ulong>("INTEGER", nullable: false),
                UserId = table.Column<ulong>("INTEGER", nullable: false),
                IsJoin = table.Column<bool>("INTEGER", nullable: false),
                DateAdded = table.Column<DateTime>("TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JoinLeaveLogs", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "JoinLeaveLogs");
    }
}