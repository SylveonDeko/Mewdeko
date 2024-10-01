using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class AddUserRoleStatesAndRoleStateSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "UserRoleStates",
            table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                GuildId = table.Column<ulong>(nullable: false),
                UserId = table.Column<ulong>(nullable: false),
                UserName = table.Column<string>(nullable: true),
                SavedRoles = table.Column<string>(nullable: true),
                DateAdded = table.Column<DateTime>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoleStates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "RoleStateSettings",
            table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                GuildId = table.Column<ulong>(nullable: false),
                Enabled = table.Column<bool>(nullable: false),
                ClearOnBan = table.Column<bool>(nullable: false),
                IgnoreBots = table.Column<bool>(nullable: false),
                DeniedRoles = table.Column<string>(nullable: true),
                DeniedUsers = table.Column<string>(nullable: true),
                DateAdded = table.Column<DateTime>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleStateSettings", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "UserRoleStates");

        migrationBuilder.DropTable(
            "RoleStateSettings");
    }
}