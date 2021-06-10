using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class antialt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AntiAltSetting",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildConfigId = table.Column<int>(nullable: false),
                    MinAge = table.Column<TimeSpan>(nullable: false),
                    Action = table.Column<int>(nullable: false),
                    ActionDurationMinutes = table.Column<int>(nullable: false),
                    RoleId = table.Column<ulong>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntiAltSetting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AntiAltSetting_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AntiAltSetting_GuildConfigId",
                table: "AntiAltSetting",
                column: "GuildConfigId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AntiAltSetting");
        }
    }
}
