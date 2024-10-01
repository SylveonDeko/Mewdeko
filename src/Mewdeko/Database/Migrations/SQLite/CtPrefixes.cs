using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class CtPrefixes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("PrefixType", "ChatTriggers", defaultValue: 0, nullable: false);
        migrationBuilder.AddColumn<string>("CustomPrefix", "ChatTriggers", defaultValue: "", nullable: false);
    }
}