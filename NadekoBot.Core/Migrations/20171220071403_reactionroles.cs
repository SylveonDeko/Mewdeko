using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace NadekoBot.Migrations
{
    public partial class reactionroles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReactionRoleMessage",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<ulong>(nullable: false),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Exclusive = table.Column<bool>(nullable: false),
                    GuildConfigId = table.Column<int>(nullable: false),
                    Index = table.Column<int>(nullable: false),
                    MessageId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionRoleMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReactionRoleMessage_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReactionRole",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    EmoteName = table.Column<string>(nullable: true),
                    ReactionRoleMessageId = table.Column<int>(nullable: true),
                    RoleId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReactionRole_ReactionRoleMessage_ReactionRoleMessageId",
                        column: x => x.ReactionRoleMessageId,
                        principalTable: "ReactionRoleMessage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReactionRole_ReactionRoleMessageId",
                table: "ReactionRole",
                column: "ReactionRoleMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ReactionRoleMessage_GuildConfigId",
                table: "ReactionRoleMessage",
                column: "GuildConfigId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReactionRole");

            migrationBuilder.DropTable(
                name: "ReactionRoleMessage");
        }
    }
}
