using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class UserPronounOverride : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Pronouns", "DiscordUser", defaultValue: "", nullable: false);
        migrationBuilder.AddColumn<string>("PronounsClearedReason", "DiscordUser", nullable: true);
        migrationBuilder.AddColumn<bool>("PronounsDisabled", "DiscordUser", nullable: false, defaultValue: false);
    }
}