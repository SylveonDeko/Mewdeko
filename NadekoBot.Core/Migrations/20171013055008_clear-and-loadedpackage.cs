using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace NadekoBot.Migrations
{
    public partial class clearandloadedpackage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClashCallers");

            migrationBuilder.DropTable(
                name: "ModulePrefixes");

            migrationBuilder.DropTable(
                name: "ClashOfClans");

            migrationBuilder.CreateTable(
                name: "LoadedPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(type: "INTEGER", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoadedPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoadedPackages_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoadedPackages_BotConfigId",
                table: "LoadedPackages",
                column: "BotConfigId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoadedPackages");

            migrationBuilder.CreateTable(
                name: "ClashOfClans",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<ulong>(nullable: false),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    EnemyClan = table.Column<string>(nullable: true),
                    GuildId = table.Column<ulong>(nullable: false),
                    Size = table.Column<int>(nullable: false),
                    StartedAt = table.Column<DateTime>(nullable: false),
                    WarState = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClashOfClans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModulePrefixes",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(nullable: true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    ModuleName = table.Column<string>(nullable: true),
                    Prefix = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulePrefixes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModulePrefixes_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClashCallers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseDestroyed = table.Column<bool>(nullable: false),
                    CallUser = table.Column<string>(nullable: true),
                    ClashWarId = table.Column<int>(nullable: false),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    SequenceNumber = table.Column<int>(nullable: true),
                    Stars = table.Column<int>(nullable: false),
                    TimeAdded = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClashCallers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClashCallers_ClashOfClans_ClashWarId",
                        column: x => x.ClashWarId,
                        principalTable: "ClashOfClans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClashCallers_ClashWarId",
                table: "ClashCallers",
                column: "ClashWarId");

            migrationBuilder.CreateIndex(
                name: "IX_ModulePrefixes_BotConfigId",
                table: "ModulePrefixes",
                column: "BotConfigId");
        }
    }
}
