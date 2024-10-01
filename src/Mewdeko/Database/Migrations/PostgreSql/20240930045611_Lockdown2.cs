#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class Lockdown2 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            "RoleId",
            "LockdownChannelPermissions",
            "TargetId");

        migrationBuilder.AddColumn<int>(
            "TargetType",
            "LockdownChannelPermissions",
            "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "TargetType",
            "LockdownChannelPermissions");

        migrationBuilder.RenameColumn(
            "TargetId",
            "LockdownChannelPermissions",
            "RoleId");
    }
}