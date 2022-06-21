using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class SlashChatTriggers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ulong>("ApplicationCommandId", "ChatTriggers", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("ApplicationCommandType", "ChatTriggers", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<bool>("EphemeralResponse", "ChatTriggers", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("ApplicationCommandName", "ChatTriggers", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("ApplicationCommandDescription", "ChatTriggers", nullable: false, defaultValue: "");
    }
}