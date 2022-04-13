using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class SuggestStateIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ulong>("StateChangeMessageId", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<int>("CurrentState", "Suggestions", defaultValue: 0);
    }
}