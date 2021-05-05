using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class addrolepunishment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "RoleId",
                table: "WarningPunishment",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UnroleTimer",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    UserId = table.Column<ulong>(nullable: false),
                    RoleId = table.Column<ulong>(nullable: false),
                    UnbanAt = table.Column<DateTime>(nullable: false),
                    GuildConfigId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnroleTimer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnroleTimer_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnroleTimer_GuildConfigId",
                table: "UnroleTimer",
                column: "GuildConfigId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnroleTimer");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "WarningPunishment");
        }
    }
}
