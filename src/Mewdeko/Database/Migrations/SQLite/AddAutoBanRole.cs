using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class AddAutoBanRole : Migration
{
    /// <inheritdoc />
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AutoBanRoles",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true, defaultValue: DateTime.UtcNow),
                GuildId = table.Column<ulong>(nullable: false),
                RoleId = table.Column<ulong>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoBanRoles", x => x.Id);
            });
    }
}