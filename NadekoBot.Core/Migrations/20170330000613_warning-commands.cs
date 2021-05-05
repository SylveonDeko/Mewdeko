using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class warningcommands : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WarningsInitialized",
                table: "GuildConfigs",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Warnings",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Forgiven = table.Column<bool>(nullable: false),
                    ForgivenBy = table.Column<string>(nullable: true),
                    GuildId = table.Column<ulong>(nullable: false),
                    Moderator = table.Column<string>(nullable: true),
                    Reason = table.Column<string>(nullable: true),
                    UserId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warnings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarningPunishment",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Count = table.Column<int>(nullable: false),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    GuildConfigId = table.Column<int>(nullable: true),
                    Punishment = table.Column<int>(nullable: false),
                    Time = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarningPunishment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarningPunishment_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarningPunishment_GuildConfigId",
                table: "WarningPunishment",
                column: "GuildConfigId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Warnings");

            migrationBuilder.DropTable(
                name: "WarningPunishment");

            migrationBuilder.DropColumn(
                name: "WarningsInitialized",
                table: "GuildConfigs");
        }
    }
}
