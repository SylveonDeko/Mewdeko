using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.PostgreSql;

public partial class MoreCurrencyShit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrencyEmote",
            table: "OwnerOnly",
            nullable: false,
            defaultValue: "💰"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CurrencyEmote",
            table: "OwnerOnly"
        );
    }
}