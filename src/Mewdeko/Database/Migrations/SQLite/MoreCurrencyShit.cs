using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class MoreCurrencyShit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("CurrencyEmote", "OwnerOnly", "TEXT", nullable: false, defaultValue: "💰");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("CurrencyEmote", "OwnerOnly");
    }
}