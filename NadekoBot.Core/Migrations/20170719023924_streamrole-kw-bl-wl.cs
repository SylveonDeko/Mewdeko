using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class streamrolekwblwl : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "StreamRoleSettings",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Keyword",
                table: "StreamRoleSettings",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StreamRoleBlacklistedUser",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    StreamRoleSettingsId = table.Column<int>(nullable: true),
                    UserId = table.Column<ulong>(nullable: false),
                    Username = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamRoleBlacklistedUser", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamRoleBlacklistedUser_StreamRoleSettings_StreamRoleSettingsId",
                        column: x => x.StreamRoleSettingsId,
                        principalTable: "StreamRoleSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StreamRoleWhitelistedUser",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    StreamRoleSettingsId = table.Column<int>(nullable: true),
                    UserId = table.Column<ulong>(nullable: false),
                    Username = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamRoleWhitelistedUser", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamRoleWhitelistedUser_StreamRoleSettings_StreamRoleSettingsId",
                        column: x => x.StreamRoleSettingsId,
                        principalTable: "StreamRoleSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreamRoleBlacklistedUser_StreamRoleSettingsId",
                table: "StreamRoleBlacklistedUser",
                column: "StreamRoleSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamRoleWhitelistedUser_StreamRoleSettingsId",
                table: "StreamRoleWhitelistedUser",
                column: "StreamRoleSettingsId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreamRoleBlacklistedUser");

            migrationBuilder.DropTable(
                name: "StreamRoleWhitelistedUser");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "StreamRoleSettings");

            migrationBuilder.DropColumn(
                name: "Keyword",
                table: "StreamRoleSettings");
        }
    }
}
