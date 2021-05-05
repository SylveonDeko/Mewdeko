using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class slowmodewhitelist : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SlowmodeIgnoredRole",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    GuildConfigId = table.Column<int>(nullable: true),
                    RoleId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlowmodeIgnoredRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlowmodeIgnoredRole_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SlowmodeIgnoredUser",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    GuildConfigId = table.Column<int>(nullable: true),
                    UserId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlowmodeIgnoredUser", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlowmodeIgnoredUser_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlowmodeIgnoredRole_GuildConfigId",
                table: "SlowmodeIgnoredRole",
                column: "GuildConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_SlowmodeIgnoredUser_GuildConfigId",
                table: "SlowmodeIgnoredUser",
                column: "GuildConfigId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlowmodeIgnoredRole");

            migrationBuilder.DropTable(
                name: "SlowmodeIgnoredUser");
        }
    }
}
