using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class musicsettingspersistence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MusicSettings");

            migrationBuilder.CreateTable(
                name: "MusicPlayerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(nullable: false),
                    PlayerRepeat = table.Column<int>(nullable: false),
                    MusicChannelId = table.Column<ulong>(nullable: true),
                    Volume = table.Column<int>(nullable: false, defaultValue: 100)
                        .Annotation("Sqlite:Autoincrement", true),
                    AutoDisconnect = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicPlayerSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MusicPlayerSettings_GuildId",
                table: "MusicPlayerSettings",
                column: "GuildId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MusicPlayerSettings");

            migrationBuilder.CreateTable(
                name: "MusicSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GuildConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    MusicChannelId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    SongAutoDelete = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MusicSettings_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MusicSettings_GuildConfigId",
                table: "MusicSettings",
                column: "GuildConfigId",
                unique: true);
        }
    }
}
