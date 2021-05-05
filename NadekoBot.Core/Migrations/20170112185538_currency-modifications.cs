using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class currencymodifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "BetflipMultiplier",
                table: "BotConfig",
                nullable: false,
                defaultValue: 1.95f);

            migrationBuilder.AddColumn<float>(
                name: "Betroll100Multiplier",
                table: "BotConfig",
                nullable: false,
                defaultValue: 10f);

            migrationBuilder.AddColumn<float>(
                name: "Betroll67Multiplier",
                table: "BotConfig",
                nullable: false,
                defaultValue: 2f);

            migrationBuilder.AddColumn<float>(
                name: "Betroll91Multiplier",
                table: "BotConfig",
                nullable: false,
                defaultValue: 4f);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyDropAmount",
                table: "BotConfig",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MinimumBetAmount",
                table: "BotConfig",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "TriviaCurrencyReward",
                table: "BotConfig",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CommandPrice",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(nullable: true),
                    CommandName = table.Column<string>(nullable: true),
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandPrice");

            migrationBuilder.DropColumn(
                name: "BetflipMultiplier",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "Betroll100Multiplier",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "Betroll67Multiplier",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "Betroll91Multiplier",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "CurrencyDropAmount",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "MinimumBetAmount",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "TriviaCurrencyReward",
                table: "BotConfig");
        }
    }
}
