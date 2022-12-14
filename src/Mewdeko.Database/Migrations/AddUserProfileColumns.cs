using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddUserProfileColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Bio", "DiscordUser", nullable: true);
        migrationBuilder.AddColumn<string>("ZodiacSign", "DiscordUser", nullable: true);
        migrationBuilder.AddColumn<string>("ProfileImageUrl", "DiscordUser", nullable: true);
        migrationBuilder.AddColumn<int>("ProfilePrivacy", "DiscordUser", defaultValue: 0);
        migrationBuilder.AddColumn<uint>("ProfileColor", "DiscordUser", defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>("Birthday", "DiscordUser", nullable: true);
        migrationBuilder.AddColumn<int>("BirthdayDisplayMode", "DiscordUser", defaultValue: 0);
    }
}