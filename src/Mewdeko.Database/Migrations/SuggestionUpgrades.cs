using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class SuggestionUpgrades : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("SuggestionThreadType", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<bool>("ArchiveOnDeny", "GuildConfigs", defaultValue: false);
        migrationBuilder.AddColumn<bool>("ArchiveOnAccept", "GuildConfigs", defaultValue: false);
        migrationBuilder.AddColumn<bool>("ArchiveOnConsider", "GuildConfigs", defaultValue: false);
        migrationBuilder.AddColumn<bool>("ArchiveOnImplement", "GuildConfigs", defaultValue: false);
        migrationBuilder.AddColumn<string>("SuggestButtonMessage", "GuildConfigs", defaultValue: "-");
        migrationBuilder.AddColumn<string>("SuggestButtonName", "GuildConfigs", defaultValue: "-");
        migrationBuilder.AddColumn<string>("SuggestButtonEmote", "GuildConfigs", defaultValue: "-");
        migrationBuilder.AddColumn<int>("ButtonRepostThreshold", "GuildConfigs", defaultValue: 5);
        migrationBuilder.AddColumn<int>("SuggestCommandsType", "GuildConfigs", defaultValue: 0);
    }
}