using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class discordpermoverride : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscordPermOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Perm = table.Column<ulong>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: true),
                    Command = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordPermOverrides", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscordPermOverrides_GuildId_Command",
                table: "DiscordPermOverrides",
                columns: new[] { "GuildId", "Command" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscordPermOverrides");
        }
    }
}
