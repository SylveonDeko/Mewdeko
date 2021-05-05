using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class curtrsimprov : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CurrencyTransactions_DateAdded",
                table: "CurrencyTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyTransactions_UserId",
                table: "CurrencyTransactions",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CurrencyTransactions_UserId",
                table: "CurrencyTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyTransactions_DateAdded",
                table: "CurrencyTransactions",
                column: "DateAdded");
        }
    }
}
