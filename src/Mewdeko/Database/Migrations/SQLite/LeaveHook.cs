using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class LeaveHook : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "LeaveHook",
            "GuildConfigs",
            "TEXT",
            defaultValue: 0,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            "AfkDel",
            "GuildConfigs",
            "TEXT",
            defaultValue: 0,
            nullable: true);
        migrationBuilder.RenameColumn("WebhookURL", "GuildConfigs", "GreetHook");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "GuildConfigs",
            "LeaveHook");
        migrationBuilder.DropColumn(
            "GuildConfigs",
            "AfkDel");
    }
}