#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class MessageCountsAddition1 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            "MinMessageLength",
            "GuildConfigs",
            "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "MinMessageLength",
            "GuildConfigs");
    }
}