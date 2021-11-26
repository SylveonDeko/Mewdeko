using LinqToDB.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class AddWinnersToGiveawayTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>("Winners", "Giveaways", nullable: true);

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}