using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class indexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Warnings_DateAdded",
                table: "Warnings",
                column: "DateAdded");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_GuildId",
                table: "Warnings",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_UserId",
                table: "Warnings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WaifuInfo_Price",
                table: "WaifuInfo",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_AwardedXp",
                table: "UserXpStats",
                column: "AwardedXp");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_GuildId",
                table: "UserXpStats",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_UserId",
                table: "UserXpStats",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_Xp",
                table: "UserXpStats",
                column: "Xp");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_DateAdded",
                table: "Reminders",
                column: "DateAdded");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_GuildId",
                table: "Quotes",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Keyword",
                table: "Quotes",
                column: "Keyword");

            migrationBuilder.CreateIndex(
                name: "IX_Donators_Amount",
                table: "Donators",
                column: "Amount");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordUser_CurrencyAmount",
                table: "DiscordUser",
                column: "CurrencyAmount");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordUser_TotalXp",
                table: "DiscordUser",
                column: "TotalXp");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordUser_UserId",
                table: "DiscordUser",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyTransactions_DateAdded",
                table: "CurrencyTransactions",
                column: "DateAdded");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Warnings_DateAdded",
                table: "Warnings");

            migrationBuilder.DropIndex(
                name: "IX_Warnings_GuildId",
                table: "Warnings");

            migrationBuilder.DropIndex(
                name: "IX_Warnings_UserId",
                table: "Warnings");

            migrationBuilder.DropIndex(
                name: "IX_WaifuInfo_Price",
                table: "WaifuInfo");

            migrationBuilder.DropIndex(
                name: "IX_UserXpStats_AwardedXp",
                table: "UserXpStats");

            migrationBuilder.DropIndex(
                name: "IX_UserXpStats_GuildId",
                table: "UserXpStats");

            migrationBuilder.DropIndex(
                name: "IX_UserXpStats_UserId",
                table: "UserXpStats");

            migrationBuilder.DropIndex(
                name: "IX_UserXpStats_Xp",
                table: "UserXpStats");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_DateAdded",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_GuildId",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_Keyword",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Donators_Amount",
                table: "Donators");

            migrationBuilder.DropIndex(
                name: "IX_DiscordUser_CurrencyAmount",
                table: "DiscordUser");

            migrationBuilder.DropIndex(
                name: "IX_DiscordUser_TotalXp",
                table: "DiscordUser");

            migrationBuilder.DropIndex(
                name: "IX_DiscordUser_UserId",
                table: "DiscordUser");

            migrationBuilder.DropIndex(
                name: "IX_CurrencyTransactions_DateAdded",
                table: "CurrencyTransactions");
        }
    }
}
