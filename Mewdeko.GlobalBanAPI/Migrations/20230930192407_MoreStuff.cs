using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mewdeko.GlobalBanAPI.Migrations
{
    /// <inheritdoc />
    public partial class MoreStuff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "GlobalBans");

            migrationBuilder.AddColumn<ulong>(
                name: "ApprovedBy",
                table: "GlobalBans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<bool>(
                name: "IsAppealable",
                table: "GlobalBans",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "GlobalBans",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "GlobalBans");

            migrationBuilder.DropColumn(
                name: "IsAppealable",
                table: "GlobalBans");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "GlobalBans");

            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "GlobalBans",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
