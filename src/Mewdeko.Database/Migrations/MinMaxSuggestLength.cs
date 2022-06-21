using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class MinMaxSuggestionLength : Migration
{
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

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}