using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace NadekoBot.Migrations
{
    public partial class checkupdates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheckForUpdates",
                table: "BotConfig",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdate",
                table: "BotConfig",
                nullable: false,
                defaultValue: DateTime.UtcNow);

            migrationBuilder.AddColumn<string>(
                name: "UpdateString",
                table: "BotConfig",
                nullable: true);

            migrationBuilder.Sql(@"delete from followedstream where username like '%/';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckForUpdates",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "LastUpdate",
                table: "BotConfig");

            migrationBuilder.DropColumn(
                name: "UpdateString",
                table: "BotConfig");
        }
    }
}
