using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class MinMaxSuggestionLength : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            "MinSuggestLength",
            "GuildConfigs",
            "INTEGER",
            defaultValue: 4098,
            nullable: true);
        migrationBuilder.AddColumn<int>(
            "MaxSuggestLength",
            "GuildConfigs",
            "INTEGER",
            defaultValue: 4098,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}