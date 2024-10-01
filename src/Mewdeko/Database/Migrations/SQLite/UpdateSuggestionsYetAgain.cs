using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class UpdateSuggestionsYetAgain : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ulong>("SuggestButtonMessageId", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("SuggestButtonRepostThreshold", "GuildConfigs", defaultValue: 5);
    }
}