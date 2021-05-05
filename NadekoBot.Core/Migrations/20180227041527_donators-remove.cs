using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace NadekoBot.Migrations
{
    public partial class donatorsremove : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Donators");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Donators",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Amount = table.Column<int>(nullable: false),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    UserId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donators", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Donators_Amount",
                table: "Donators",
                column: "Amount");

            migrationBuilder.CreateIndex(
                name: "IX_Donators_UserId",
                table: "Donators",
                column: "UserId",
                unique: true);
        }
    }
}
