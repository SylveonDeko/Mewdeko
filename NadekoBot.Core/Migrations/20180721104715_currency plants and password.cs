using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class currencyplantsandpassword : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandPrice");

            migrationBuilder.AddColumn<bool>(
                name: "CurrencyGenerationPassword",
                table: "BotConfig",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlantedCurrency",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Amount = table.Column<long>(nullable: false),
                    Password = table.Column<string>(nullable: true),
                    GuildId = table.Column<ulong>(nullable: false),
                    ChannelId = table.Column<ulong>(nullable: false),
                    UserId = table.Column<ulong>(nullable: false),
                    MessageId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantedCurrency", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlantedCurrency_ChannelId",
                table: "PlantedCurrency",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantedCurrency_MessageId",
                table: "PlantedCurrency",
                column: "MessageId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlantedCurrency");

            migrationBuilder.DropColumn(
                name: "CurrencyGenerationPassword",
                table: "BotConfig");

            migrationBuilder.CreateTable(
                name: "CommandPrice",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(nullable: true),
                    CommandName = table.Column<string>(nullable: true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Price = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandPrice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandPrice_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandPrice_BotConfigId",
                table: "CommandPrice",
                column: "BotConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandPrice_Price",
                table: "CommandPrice",
                column: "Price",
                unique: true);
        }
    }
}
