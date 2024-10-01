#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class MesReqs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            "MessageCountReq",
            "Giveaways",
            "numeric(20,0)",
            nullable: false,
            defaultValue: 0m);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "MessageCountReq",
            "Giveaways");
    }
}