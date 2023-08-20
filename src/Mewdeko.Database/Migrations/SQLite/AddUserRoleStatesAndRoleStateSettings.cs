using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite
{
    public partial class AddUserRoleStatesAndRoleStateSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserRoleStates",
                columns: table => new
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
                name: "RoleStateSettings",
                columns: table => new
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserRoleStates");

            migrationBuilder.DropTable(
                name: "RoleStateSettings");
        }
    }
}