using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class UserPronounOverride : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Pronouns", "DiscordUser", defaultValue: "", nullable: false);
        migrationBuilder.AddColumn<string>("PronounsClearedReason", "DiscordUser", nullable: true);
        migrationBuilder.AddColumn<bool>("PronounsDisabled", "DiscordUser", nullable: false, defaultValue: false);
    }
}