using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace NadekoBot.Migrations
{
    public partial class currencydecay : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "DailyCurrencyDecay",
                table: "BotConfig",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCurrencyDecay",
                table: "BotConfig",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyCurrencyDecay",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "LastCurrencyDecay",
                table: "BotConfig");
        }
    }
}
