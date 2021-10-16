using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class Giveaways : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Giveaways",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    When = table.Column<DateTime>(nullable: true),
                    ServerId = table.Column<ulong>(nullable: false),
                    ChannelId = table.Column<ulong>(nullable: true),
                    MessageId = table.Column<ulong>(nullable: true),
                    UserId = table.Column<ulong>(nullable: true),
                    Item = table.Column<string>(nullable: true),
                    RestrictTo = table.Column<string>(nullable: true),
                    BlacklistUsers = table.Column<string>(nullable: true),
                    BlacklistRoles = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Giveaways", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Giveaways_GuildId",
                table: "Giveaways",
                column: "ServerId",
                unique: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Giveaways");
        }
    }
}