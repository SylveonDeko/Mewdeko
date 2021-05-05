using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class gmodandcmod : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockedCmdOrMdl",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(nullable: true),
                    BotConfigId1 = table.Column<int>(nullable: true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedCmdOrMdl", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlockedCmdOrMdl_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BlockedCmdOrMdl_BotConfig_BotConfigId1",
                        column: x => x.BotConfigId1,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockedCmdOrMdl_BotConfigId",
                table: "BlockedCmdOrMdl",
                column: "BotConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_BlockedCmdOrMdl_BotConfigId1",
                table: "BlockedCmdOrMdl",
                column: "BotConfigId1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedCmdOrMdl");
        }
    }
}
