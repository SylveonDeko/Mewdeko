using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class ChatTriggerRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("GrantedRoles", "CustomReactions", nullable: true);
        migrationBuilder.AddColumn<string>("RemovedRoles", "CustomReactions", nullable: true);
    }
}
