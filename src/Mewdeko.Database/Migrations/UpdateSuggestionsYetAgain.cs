using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class UpdateSuggestionsYetAgain : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ulong>("SuggestButtonMessageId", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("SuggestButtonRepostThreshold", "GuildConfigs", defaultValue: 5);
    }
}