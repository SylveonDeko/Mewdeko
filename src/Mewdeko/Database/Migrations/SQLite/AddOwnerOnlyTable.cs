using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class AddOwnerOnlyTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "OwnerOnly",
            table => new
            {
                Id = table.Column<int>("INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Owners = table.Column<string>("TEXT", nullable: true),
                GptTokensUsed = table.Column<string>("TEXT", nullable: true),
                DateAdded = table.Column<DateTime>("TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OwnerOnly", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "OwnerOnly");
    }
}