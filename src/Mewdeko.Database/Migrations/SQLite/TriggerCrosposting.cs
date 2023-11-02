using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class TriggerCrossposting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ulong>("CrosspostingChannelId", "ChatTriggers", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<string>("CrosspostingWebhookUrl", "ChatTriggers", nullable: false, defaultValue: "");
    }
}