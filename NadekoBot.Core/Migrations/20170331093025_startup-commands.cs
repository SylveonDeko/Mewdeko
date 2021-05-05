using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class startupcommands : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StartupCommand",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(nullable: true),
                    ChannelId = table.Column<ulong>(nullable: false),
                    ChannelName = table.Column<string>(nullable: true),
                    CommandText = table.Column<string>(nullable: true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    GuildId = table.Column<ulong>(nullable: true),
                    GuildName = table.Column<string>(nullable: true),
                    Index = table.Column<int>(nullable: false),
                    VoiceChannelId = table.Column<ulong>(nullable: true),
                    VoiceChannelName = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StartupCommand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StartupCommand_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StartupCommand_BotConfigId",
                table: "StartupCommand",
                column: "BotConfigId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StartupCommand");
        }
    }
}
