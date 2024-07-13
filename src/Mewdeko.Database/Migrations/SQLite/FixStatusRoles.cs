using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class FixStatusRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("StatusEmbed", "StatusRoles", nullable: true);
        migrationBuilder.AddColumn<string>("StatusChannelId", "StatusRoles", nullable: true);
        migrationBuilder.AddColumn<bool>("ReaddRemoved", "StatusRoles", defaultValue: false);
        migrationBuilder.AddColumn<string>("RemoveAdded", "StatusRoles", defaultValue: true);
    }
}