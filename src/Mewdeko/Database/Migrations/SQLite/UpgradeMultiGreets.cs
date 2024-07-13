using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class UpgradeMultiGreets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<bool>("Disabled", "MultiGreets", nullable: false, defaultValue: false);
}