using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class ChatTriggerRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("GrantedRoles", "ChatTriggers", nullable: true);
        migrationBuilder.AddColumn<string>("RemovedRoles", "ChatTriggers", nullable: true);
    }
}