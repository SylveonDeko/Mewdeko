using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddWinnersToGiveawayTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>("Winners", "Giveaways", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}