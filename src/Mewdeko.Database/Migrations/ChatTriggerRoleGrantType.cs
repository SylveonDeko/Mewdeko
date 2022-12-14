using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class ChatTriggerRoleGrantType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>("RoleGrantType", "ChatTriggers", nullable: false, defaultValue: 0);
}