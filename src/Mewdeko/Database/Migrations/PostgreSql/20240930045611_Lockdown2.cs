using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class Lockdown2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RoleId",
                table: "LockdownChannelPermissions",
                newName: "TargetId");

            migrationBuilder.AddColumn<int>(
                name: "TargetType",
                table: "LockdownChannelPermissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "LockdownChannelPermissions");

            migrationBuilder.RenameColumn(
                name: "TargetId",
                table: "LockdownChannelPermissions",
                newName: "RoleId");
        }
    }
}
