using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class permsv2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Permissionv2",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    GuildConfigId = table.Column<int>(nullable: true),
                    Index = table.Column<int>(nullable: false),
                    PrimaryTarget = table.Column<int>(nullable: false),
                    PrimaryTargetId = table.Column<ulong>(nullable: false),
                    SecondaryTarget = table.Column<int>(nullable: false),
                    SecondaryTargetName = table.Column<string>(nullable: true),
                    State = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissionv2", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permissionv2_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissionv2_GuildConfigId",
                table: "Permissionv2",
                column: "GuildConfigId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Permissionv2");
        }
    }
}
