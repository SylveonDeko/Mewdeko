using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class repeattimeofday : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "StartTimeOfDay",
                table: "GuildRepeater",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartTimeOfDay",
                table: "GuildRepeater");
        }
    }
}
