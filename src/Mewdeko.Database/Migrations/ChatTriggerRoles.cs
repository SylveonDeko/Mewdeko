using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class ChatTriggerRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("GrantedRoles", "ChatTriggers", nullable: true);
        migrationBuilder.AddColumn<string>("RemovedRoles", "ChatTriggers", nullable: true);
    }
}